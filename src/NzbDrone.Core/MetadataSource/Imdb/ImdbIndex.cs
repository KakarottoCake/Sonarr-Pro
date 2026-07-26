using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.EnvironmentInfo;

namespace NzbDrone.Core.MetadataSource.Imdb
{
    public interface IImdbIndex
    {
        bool Exists();
        DateTime? LastBuilt();
        List<ImdbEpisodeEntry> GetEpisodes(int parentTconst);
        void Rebuild(Func<IImdbIndexWriter, DateTime> build);
    }

    public interface IImdbIndexWriter
    {
        void WriteSeries(int parentTconst, IReadOnlyList<ImdbEpisodeEntry> episodes);
    }

    /// <summary>
    /// A local copy of the one thing this fork uses IMDb's published dumps for: which
    /// episodes a series has and how they are numbered.
    /// <para>
    /// Kept in its own database rather than the main one. It is a derived cache that can be
    /// rebuilt from IMDb at any time, so it should not be carried in backups or bloat the
    /// file everything else depends on.
    /// </para>
    /// <para>
    /// Ratings are deliberately not held. The dump is small compressed but expands to more
    /// than a million rows, which doubled the index for data every metadata provider already
    /// supplies for the handful of series actually in a library.
    /// </para>
    /// </summary>
    public class ImdbIndex : IImdbIndex
    {
        private const string EpisodeTable = "SeriesEpisodes";
        private const string MetaTable = "IndexMeta";

        private readonly IAppFolderInfo _appFolderInfo;
        private readonly IDiskProvider _diskProvider;
        private readonly Logger _logger;
        private readonly object _lock = new object();

        public ImdbIndex(IAppFolderInfo appFolderInfo, IDiskProvider diskProvider, Logger logger)
        {
            _appFolderInfo = appFolderInfo;
            _diskProvider = diskProvider;
            _logger = logger;
        }

        private string DatabasePath => Path.Combine(_appFolderInfo.AppDataFolder, "imdb.db");

        private string ConnectionString => new SQLiteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            CacheSize = -20000,
            JournalMode = SQLiteJournalModeEnum.Wal,
            SyncMode = SynchronizationModes.Off
        }.ConnectionString;

        public bool Exists()
        {
            return _diskProvider.FileExists(DatabasePath);
        }

        public DateTime? LastBuilt()
        {
            if (!Exists())
            {
                return null;
            }

            try
            {
                using var connection = Open();
                using var command = connection.CreateCommand();

                command.CommandText = $"SELECT \"Value\" FROM \"{MetaTable}\" WHERE \"Key\" = 'BuiltAt'";

                var value = command.ExecuteScalar() as string;

                return value != null && DateTime.TryParse(value, out var parsed) ? parsed : null;
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to read the IMDb index timestamp");

                return null;
            }
        }

        public List<ImdbEpisodeEntry> GetEpisodes(int parentTconst)
        {
            if (!Exists() || parentTconst <= 0)
            {
                return new List<ImdbEpisodeEntry>();
            }

            try
            {
                using var connection = Open();
                using var command = connection.CreateCommand();

                command.CommandText = $"SELECT \"Packed\" FROM \"{EpisodeTable}\" WHERE \"ParentTconst\" = @parent";
                command.Parameters.AddWithValue("@parent", parentTconst);

                return ImdbEpisodePacker.Unpack(command.ExecuteScalar() as byte[]);
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Unable to read IMDb episodes for {0}", parentTconst);

                return new List<ImdbEpisodeEntry>();
            }
        }

        /// <summary>
        /// Builds a fresh index alongside the current one and swaps it in only once it is
        /// complete, so an interrupted rebuild leaves the existing index usable rather than
        /// half-replaced.
        /// </summary>
        public void Rebuild(Func<IImdbIndexWriter, DateTime> build)
        {
            lock (_lock)
            {
                var temporaryPath = DatabasePath + ".building";

                if (_diskProvider.FileExists(temporaryPath))
                {
                    _diskProvider.DeleteFile(temporaryPath);
                }

                try
                {
                    DateTime builtAt;

                    using (var connection = Open(temporaryPath))
                    {
                        Create(connection);

                        using var transaction = connection.BeginTransaction();

                        var writer = new Writer(connection, transaction);

                        builtAt = build(writer);
                        writer.Flush();

                        using (var meta = connection.CreateCommand())
                        {
                            meta.Transaction = transaction;
                            meta.CommandText = $"INSERT OR REPLACE INTO \"{MetaTable}\" (\"Key\", \"Value\") VALUES ('BuiltAt', @value)";
                            meta.Parameters.AddWithValue("@value", builtAt.ToString("o"));
                            meta.ExecuteNonQuery();
                        }

                        transaction.Commit();
                    }

                    // Pooled connections keep the file handle open, which blocks the move.
                    SQLiteConnection.ClearAllPools();

                    if (_diskProvider.FileExists(DatabasePath))
                    {
                        _diskProvider.DeleteFile(DatabasePath);
                    }

                    _diskProvider.MoveFile(temporaryPath, DatabasePath);

                    _logger.Info("IMDb index rebuilt");
                }
                catch
                {
                    SQLiteConnection.ClearAllPools();

                    if (_diskProvider.FileExists(temporaryPath))
                    {
                        _diskProvider.DeleteFile(temporaryPath);
                    }

                    throw;
                }
            }
        }

        private SQLiteConnection Open(string path = null)
        {
            var connectionString = path == null
                ? ConnectionString
                : new SQLiteConnectionStringBuilder
                {
                    DataSource = path,
                    JournalMode = SQLiteJournalModeEnum.Off,
                    SyncMode = SynchronizationModes.Off
                }.ConnectionString;

            var connection = new SQLiteConnection(connectionString);
            connection.Open();

            return connection;
        }

        private static void Create(SQLiteConnection connection)
        {
            using var command = connection.CreateCommand();

            command.CommandText = $@"
                CREATE TABLE ""{EpisodeTable}"" (""ParentTconst"" INTEGER PRIMARY KEY, ""Packed"" BLOB NOT NULL);
                CREATE TABLE ""{MetaTable}"" (""Key"" TEXT PRIMARY KEY, ""Value"" TEXT NOT NULL);";

            command.ExecuteNonQuery();
        }

        private sealed class Writer : IImdbIndexWriter
        {
            private readonly SQLiteCommand _episodeCommand;

            public Writer(SQLiteConnection connection, SQLiteTransaction transaction)
            {
                _episodeCommand = connection.CreateCommand();
                _episodeCommand.Transaction = transaction;
                _episodeCommand.CommandText = $"INSERT OR REPLACE INTO \"{EpisodeTable}\" (\"ParentTconst\", \"Packed\") VALUES (@parent, @packed)";
                _episodeCommand.Parameters.Add("@parent", System.Data.DbType.Int32);
                _episodeCommand.Parameters.Add("@packed", System.Data.DbType.Binary);
            }

            public void WriteSeries(int parentTconst, IReadOnlyList<ImdbEpisodeEntry> episodes)
            {
                _episodeCommand.Parameters["@parent"].Value = parentTconst;
                _episodeCommand.Parameters["@packed"].Value = ImdbEpisodePacker.Pack(episodes);
                _episodeCommand.ExecuteNonQuery();
            }

            public void Flush()
            {
                _episodeCommand.Dispose();
            }
        }
    }
}

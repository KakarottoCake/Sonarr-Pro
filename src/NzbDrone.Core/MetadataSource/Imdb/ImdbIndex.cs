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
        /// <summary>
        /// Stages one row. Rows may arrive in any order; <see cref="PackStagedRows"/> groups
        /// them afterwards.
        /// </summary>
        void StageEpisode(int parentTconst, ImdbEpisodeEntry episode);

        /// <summary>
        /// Groups the staged rows by series and packs each into its blob.
        /// </summary>
        void PackStagedRows();
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
        private const string StagingTable = "StagingEpisodes";

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

                        // Dropping the staging table frees its pages inside the file but
                        // does not shrink the file, which leaves it several times larger
                        // than the data it holds. VACUUM rewrites it compactly, and cannot
                        // run inside a transaction.
                        using var vacuum = connection.CreateCommand();

                        vacuum.CommandText = "VACUUM";
                        vacuum.ExecuteNonQuery();
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
                CREATE TABLE ""{MetaTable}"" (""Key"" TEXT PRIMARY KEY, ""Value"" TEXT NOT NULL);
                CREATE TABLE ""{StagingTable}"" (""ParentTconst"" INTEGER NOT NULL, ""Tconst"" INTEGER NOT NULL, ""SeasonNumber"" INTEGER NOT NULL, ""EpisodeNumber"" INTEGER NOT NULL);";

            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Stages rows to disk and groups them with a sorted read afterwards.
        /// <para>
        /// The dump is ordered by episode id rather than by series, so a series' rows are
        /// scattered through it and cannot be grouped as they stream past. Collecting them
        /// in a dictionary first works but holds nine million entries at once, which trebles
        /// the process's memory for the duration and is a poor thing to do on the small
        /// machines this tends to run on. Letting SQLite do the sort keeps the peak to a
        /// single series.
        /// </para>
        /// </summary>
        private sealed class Writer : IImdbIndexWriter
        {
            private readonly SQLiteConnection _connection;
            private readonly SQLiteTransaction _transaction;
            private readonly SQLiteCommand _stageCommand;

            public Writer(SQLiteConnection connection, SQLiteTransaction transaction)
            {
                _connection = connection;
                _transaction = transaction;

                _stageCommand = connection.CreateCommand();
                _stageCommand.Transaction = transaction;
                _stageCommand.CommandText = $"INSERT INTO \"{StagingTable}\" (\"ParentTconst\", \"Tconst\", \"SeasonNumber\", \"EpisodeNumber\") VALUES (@parent, @tconst, @season, @episode)";
                _stageCommand.Parameters.Add("@parent", System.Data.DbType.Int32);
                _stageCommand.Parameters.Add("@tconst", System.Data.DbType.Int32);
                _stageCommand.Parameters.Add("@season", System.Data.DbType.Int32);
                _stageCommand.Parameters.Add("@episode", System.Data.DbType.Int32);
            }

            public void StageEpisode(int parentTconst, ImdbEpisodeEntry episode)
            {
                _stageCommand.Parameters["@parent"].Value = parentTconst;
                _stageCommand.Parameters["@tconst"].Value = episode.Tconst;
                _stageCommand.Parameters["@season"].Value = episode.SeasonNumber;
                _stageCommand.Parameters["@episode"].Value = episode.EpisodeNumber;
                _stageCommand.ExecuteNonQuery();
            }

            public void PackStagedRows()
            {
                using var insert = _connection.CreateCommand();

                insert.Transaction = _transaction;
                insert.CommandText = $"INSERT OR REPLACE INTO \"{EpisodeTable}\" (\"ParentTconst\", \"Packed\") VALUES (@parent, @packed)";
                insert.Parameters.Add("@parent", System.Data.DbType.Int32);
                insert.Parameters.Add("@packed", System.Data.DbType.Binary);

                using var select = _connection.CreateCommand();

                select.Transaction = _transaction;

                // Sorted by season and episode within each series so the packed order is the
                // reading order, and so consecutive ids compress well.
                select.CommandText = $"SELECT \"ParentTconst\", \"Tconst\", \"SeasonNumber\", \"EpisodeNumber\" FROM \"{StagingTable}\" ORDER BY \"ParentTconst\", \"SeasonNumber\", \"EpisodeNumber\"";

                var current = 0;
                var episodes = new List<ImdbEpisodeEntry>();

                using (var reader = select.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        var parent = reader.GetInt32(0);

                        if (parent != current)
                        {
                            Write(insert, current, episodes);

                            current = parent;
                            episodes.Clear();
                        }

                        episodes.Add(new ImdbEpisodeEntry(reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3)));
                    }
                }

                Write(insert, current, episodes);

                using var drop = _connection.CreateCommand();

                drop.Transaction = _transaction;
                drop.CommandText = $"DROP TABLE \"{StagingTable}\"";
                drop.ExecuteNonQuery();
            }

            private static void Write(SQLiteCommand insert, int parentTconst, List<ImdbEpisodeEntry> episodes)
            {
                if (parentTconst == 0 || episodes.Count == 0)
                {
                    return;
                }

                insert.Parameters["@parent"].Value = parentTconst;
                insert.Parameters["@packed"].Value = ImdbEpisodePacker.Pack(episodes);
                insert.ExecuteNonQuery();
            }

            public void Flush()
            {
                _stageCommand.Dispose();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.LibraryTools
{
    public interface IRetainedVersionService
    {
        string Preserve(EpisodeFile file, Series series, List<int> episodeIds);
        List<RetainedVersion> List(int seriesId);
        void Activate(int seriesId, int versionId);
    }

    public class RetainedVersionService : IRetainedVersionService, IExecute<ActivateRetainedVersionCommand>
    {
        private static readonly object FileGate = new();
        private readonly IRetainedVersionRepository _repository;
        private readonly IDiskProvider _disk;
        private readonly IDiskTransferService _transfer;
        private readonly ISeriesService _series;
        private readonly IEpisodeService _episodes;
        private readonly IMediaFileService _files;

        public RetainedVersionService(IRetainedVersionRepository repository,
            IDiskProvider disk,
            IDiskTransferService transfer,
            ISeriesService series,
            IEpisodeService episodes,
            IMediaFileService files)
        {
            _repository = repository;
            _disk = disk;
            _transfer = transfer;
            _series = series;
            _episodes = episodes;
            _files = files;
        }

        public string Preserve(EpisodeFile file, Series series, List<int> episodeIds)
        {
            lock (FileGate)
            {
                var original = ArchivePathGuard.Resolve(series.Path, file.RelativePath);
                var relativePath = Path.Combine("Plex Versions", Guid.NewGuid().ToString("N"), Path.GetFileName(file.RelativePath));
                var destination = ArchivePathGuard.Resolve(series.Path, relativePath);
                var metadata = new EpisodeFile
                {
                    SeriesId = series.Id, SeasonNumber = file.SeasonNumber, RelativePath = file.RelativePath,
                    Size = file.Size, DateAdded = file.DateAdded, OriginalFilePath = file.OriginalFilePath,
                    SceneName = file.SceneName, ReleaseGroup = file.ReleaseGroup, ReleaseHash = file.ReleaseHash,
                    Quality = file.Quality, IndexerFlags = file.IndexerFlags, MediaInfo = file.MediaInfo,
                    Languages = file.Languages, ReleaseType = file.ReleaseType,
                    SourceReleaseSize = file.SourceReleaseSize, SourceReleaseTitle = file.SourceReleaseTitle
                };
                _disk.CreateFolder(Path.GetDirectoryName(destination));

                // Persist the recovery record first. A failed move leaves the original intact;
                // a restart between steps leaves a visible unavailable archive rather than lost media.
                var record = _repository.Insert(new RetainedVersion
                {
                    SeriesId = series.Id, RelativePath = relativePath, MetadataJson = metadata.ToJson(),
                    EpisodeIdsJson = episodeIds.Distinct().ToList().ToJson(), Created = DateTime.UtcNow
                });
                try
                {
                    _transfer.TransferFile(original, destination, TransferMode.Move);
                }
                catch
                {
                    _repository.Delete(record.Id);
                    throw;
                }

                return destination;
            }
        }

        public List<RetainedVersion> List(int seriesId) => _repository.ForSeries(seriesId);

        public void Execute(ActivateRetainedVersionCommand command) => Activate(command.SeriesId, command.VersionId);

        public void Activate(int seriesId, int versionId)
        {
            lock (FileGate)
            {
                var series = _series.GetSeries(seriesId);
                var selected = _repository.Get(versionId);
                if (selected.SeriesId != seriesId || !string.IsNullOrWhiteSpace(series.PendingPath))
                {
                    throw new IOException("Finish the series move before changing versions.");
                }

                var ids = Json.Deserialize<List<int>>(selected.EpisodeIdsJson);
                var episodes = _episodes.GetEpisodes(ids);
                if (ids.Count == 0 || episodes.Count != ids.Count || episodes.Any(e => e.SeriesId != seriesId) || episodes.Select(e => e.EpisodeFileId).Distinct().Count() != 1 || episodes[0].EpisodeFileId == 0)
                {
                    throw new IOException("These episodes do not share one active file. Use Manual Import to remap this version.");
                }

                var active = _files.Get(episodes[0].EpisodeFileId);
                var activeEpisodes = _episodes.GetEpisodesByFileId(active.Id);
                if (!activeEpisodes.Select(e => e.Id).OrderBy(i => i).SequenceEqual(ids.OrderBy(i => i)))
                {
                    throw new IOException("This version covers different episodes from the active file. Use Manual Import to remap it.");
                }

                var metadata = Json.Deserialize<EpisodeFile>(selected.MetadataJson);
                var source = ArchivePathGuard.Resolve(series.Path, selected.RelativePath);
                var destination = ArchivePathGuard.Resolve(series.Path, metadata.RelativePath);
                var activePath = ArchivePathGuard.Resolve(series.Path, active.RelativePath);
                if (!_disk.FileExists(source) || (_disk.FileExists(destination) && !string.Equals(destination, activePath, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)))
                {
                    throw new IOException("The retained file is unavailable or its destination contains another file.");
                }

                var staging = ArchivePathGuard.Resolve(series.Path, Path.Combine("Plex Versions", Guid.NewGuid().ToString("N"), Path.GetFileName(metadata.RelativePath)));
                _disk.CreateFolder(Path.GetDirectoryName(staging));
                _transfer.TransferFile(source, staging, TransferMode.Copy);
                string backup = null;
                var installed = false;
                DateTime installedDate = default;
                long installedSize = 0;
                try
                {
                    backup = Preserve(active, series, ids);
                    _disk.CreateFolder(Path.GetDirectoryName(destination));
                    _transfer.TransferFile(staging, destination, TransferMode.Move);
                    installed = true;
                    installedDate = _disk.FileGetLastWrite(destination);
                    installedSize = _disk.GetFileSize(destination);
                    metadata.Id = active.Id;
                    _files.Update(metadata);
                }
                catch
                {
                    // Recover only our unchanged installed copy, preserving unexpected
                    // concurrent writes and retaining the recovery archive either way.
                    if (installed && _disk.FileExists(destination) && _disk.GetFileSize(destination) == installedSize && _disk.FileGetLastWrite(destination) == installedDate)
                    {
                        _disk.DeleteFile(destination);
                    }

                    if (backup != null && !_disk.FileExists(activePath))
                    {
                        _transfer.TransferFile(backup, activePath, TransferMode.Copy);
                    }

                    throw;
                }
                finally
                {
                    if (_disk.FileExists(staging))
                    {
                        _disk.DeleteFile(staging);
                    }
                }
            }
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Commands;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.LibraryTools
{
    public class RecycleRestoreService : IExecute<RestoreRecycledFileCommand>
    {
        private readonly IConfigService _config;
        private readonly ISeriesService _series;
        private readonly IDiskProvider _disk;
        private readonly IDiskTransferService _transfer;
        private readonly IBuildFileNames _names;
        private readonly IManageCommandQueue _commands;

        public RecycleRestoreService(IConfigService config, ISeriesService series, IDiskProvider disk, IDiskTransferService transfer, IBuildFileNames names, IManageCommandQueue commands)
        {
            _config = config;
            _series = series;
            _disk = disk;
            _transfer = transfer;
            _names = names;
            _commands = commands;
        }

        public void Execute(RestoreRecycledFileCommand command)
        {
            var series = _series.GetSeries(command.SeriesId);
            if (!string.IsNullOrWhiteSpace(series.PendingPath) || !series.Seasons.Any(s => s.SeasonNumber == command.SeasonNumber) || !_disk.FolderExists(series.Path))
            {
                throw new IOException("Choose an existing season and wait for any series move to finish.");
            }

            var source = ArchivePathGuard.Resolve(_config.RecycleBin, command.RelativePath);
            var extensions = new HashSet<string>(MediaFileExtensions.Extensions);
            extensions.UnionWith(new[] { ".srt", ".ass", ".ssa", ".sub", ".idx", ".vtt" });
            if (!extensions.Contains(Path.GetExtension(source)) || !_disk.FileExists(source))
            {
                throw new IOException("This recycled media file is no longer available.");
            }

            var folder = series.SeasonFolder ? _names.BuildSeasonPath(series, command.SeasonNumber) : series.Path;
            var target = ArchivePathGuard.Resolve(series.Path, Path.GetRelativePath(series.Path, Path.Combine(folder, Path.GetFileName(source))));
            if (_disk.FileExists(target))
            {
                throw new IOException("A file already exists at the restore destination. It will not be overwritten.");
            }

            _disk.CreateFolder(Path.GetDirectoryName(target));
            _transfer.TransferFile(source, target, TransferMode.Move);
            _commands.Push(new RescanSeriesCommand { SeriesId = series.Id }, trigger: CommandTrigger.Manual);
        }
    }
}

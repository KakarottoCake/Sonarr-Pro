using System.Text;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.LibraryTools;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Tv;
using Sonarr.Http;

namespace Sonarr.Api.V5.LibraryTools;

[V5ApiController("librarytools")]
public class LibraryArchiveController : Controller
{
    private readonly IConfigService _config;
    private readonly ISeriesService _series;
    private readonly IDiskProvider _disk;
    private readonly IManageCommandQueue _commands;
    private readonly IRetainedVersionService _versions;

    public LibraryArchiveController(IConfigService config,
        ISeriesService series,
        IDiskProvider disk,
        IManageCommandQueue commands,
        IRetainedVersionService versions)
    {
        _config = config;
        _series = series;
        _disk = disk;
        _commands = commands;
        _versions = versions;
    }

    [HttpGet("recycle")]
    public object BrowseRecycle()
    {
        var root = _config.RecycleBin;
        if (string.IsNullOrWhiteSpace(root) || !_disk.FolderExists(root))
        {
            return new { configured = !string.IsNullOrWhiteSpace(root), path = root, files = Array.Empty<object>(), truncated = false };
        }

        var extensions = new HashSet<string>(MediaFileExtensions.Extensions);
        extensions.UnionWith(new[] { ".srt", ".ass", ".ssa", ".sub", ".idx", ".vtt" });
        var files = Directory.EnumerateFiles(root, "*", new EnumerationOptions
        {
            RecurseSubdirectories = true, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint
        }).Where(path => extensions.Contains(Path.GetExtension(path))).Take(2001).ToList();
        return new
        {
            configured = true, path = root, truncated = files.Count > 2000,
            files = files.Take(2000).Select(path =>
            {
                var relative = Path.GetRelativePath(root, path);
                var info = new FileInfo(path);
                return new { token = Convert.ToBase64String(Encoding.UTF8.GetBytes(relative)), name = relative, size = info.Length, recycledAt = info.LastWriteTimeUtc };
            }).ToList()
        };
    }

    [HttpPost("recycle/restore")]
    public async Task<IActionResult> Restore([FromBody] RecycleRestoreRequest request, CancellationToken cancellationToken)
    {
        await LibraryOperationGate.Gate.WaitAsync(cancellationToken);
        try
        {
            if (string.IsNullOrEmpty(request.Token) || request.Token.Length > 4096)
            {
                return BadRequest(new { message = "Invalid recycle-bin item." });
            }

            _series.GetSeries(request.SeriesId);
            var relative = Encoding.UTF8.GetString(Convert.FromBase64String(request.Token));
            ArchivePathGuard.Resolve(_config.RecycleBin, relative);
            if (await Series.SeriesCompressionController.HasActiveJob(request.SeriesId, cancellationToken))
            {
                return Conflict(new { message = "Wait for this show's compression to finish before restoring files." });
            }

            var command = _commands.Push(new RestoreRecycledFileCommand
            {
                RelativePath = relative, SeriesId = request.SeriesId, SeasonNumber = request.SeasonNumber, Trigger = CommandTrigger.Manual
            });
            return Accepted(new { commandId = command.Id, message = "Restore queued safely with other file operations." });
        }
        catch (Exception ex) when (ex is IOException or FormatException or UnauthorizedAccessException or ArgumentException)
        {
            return BadRequest(new { message = "Restore failed: " + ex.Message });
        }
        finally
        {
            LibraryOperationGate.Gate.Release();
        }
    }

    [HttpGet("series/{id:int}/versions")]
    public object GetVersions(int id)
    {
        var series = _series.GetSeries(id);
        return _versions.List(id).OrderByDescending(v => v.Created).Select(v => new
        {
            v.Id, v.Created, path = v.RelativePath,
            available = _disk.FileExists(ArchivePathGuard.Resolve(series.Path, v.RelativePath)),
            metadata = NzbDrone.Common.Serializer.Json.Deserialize<EpisodeFile>(v.MetadataJson),
            episodeIds = NzbDrone.Common.Serializer.Json.Deserialize<List<int>>(v.EpisodeIdsJson)
        }).ToList();
    }

    [HttpPost("series/{id:int}/versions/{versionId:int}/activate")]
    public async Task<IActionResult> ActivateVersion(int id, int versionId, CancellationToken cancellationToken)
    {
        await LibraryOperationGate.Gate.WaitAsync(cancellationToken);
        try
        {
            if (_commands.GetStarted().Any(c => c.Body.RequiresDiskAccess) || await Series.SeriesCompressionController.HasActiveJob(id, cancellationToken))
            {
                return Conflict(new { message = "Wait for active imports, moves, renames or compression to finish before changing versions." });
            }

            var command = _commands.Push(new ActivateRetainedVersionCommand { SeriesId = id, VersionId = versionId, Trigger = CommandTrigger.Manual });
            return Accepted(new { commandId = command.Id, message = "Version change queued safely with other file operations." });
        }
        catch (IOException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        finally
        {
            LibraryOperationGate.Gate.Release();
        }
    }
}

public class RecycleRestoreRequest
{
    public string Token { get; set; } = string.Empty;
    public int SeriesId { get; set; }
    public int SeasonNumber { get; set; }
}

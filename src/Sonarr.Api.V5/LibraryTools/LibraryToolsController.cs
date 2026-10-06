using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.LibraryTools;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.RootFolders;
using NzbDrone.Core.Tv;
using Sonarr.Http;

namespace Sonarr.Api.V5.LibraryTools;

[V5ApiController("librarytools")]
public class LibraryToolsController : Controller
{
    private static readonly object SettingsGate = new();
    private readonly IProOptionsService _options;
    private readonly ISeriesService _series;
    private readonly IMediaFileService _files;
    private readonly IDiskProvider _disk;
    private readonly IRootFolderService _roots;
    private readonly ITrackedDownloadService _downloads;
    private readonly IConfigService _config;
    private readonly ISearchActivityService _searchActivity;

    public LibraryToolsController(IProOptionsService options,
        ISeriesService series,
        IMediaFileService files,
        IDiskProvider disk,
        IRootFolderService roots,
        ITrackedDownloadService downloads,
        IConfigService config,
        ISearchActivityService searchActivity)
    {
        _options = options;
        _series = series;
        _files = files;
        _disk = disk;
        _roots = roots;
        _downloads = downloads;
        _config = config;
        _searchActivity = searchActivity;
    }

    [HttpGet]
    public object GetOptions() => _options.Read();

    [HttpPut]
    public object SaveOptions([FromBody] ProOptions request)
    {
        lock (SettingsGate)
        {
            var options = _options.Read();
            options.AutomationPaused = request.AutomationPaused;
            options.HardlinkOnly = request.HardlinkOnly;
            options.MatchExternalIds = request.MatchExternalIds;
            options.PreferAnimeSeasonPacks = request.PreferAnimeSeasonPacks;
            _options.Save(options);
            return _options.Read();
        }
    }

    [HttpGet("series/{id:int}")]
    public object GetSeriesOptions(int id)
    {
        _series.GetSeries(id);
        return _options.ForSeries(id);
    }

    [HttpPut("series/{id:int}")]
    public IActionResult SaveSeriesOptions(int id, [FromBody] ProSeriesOptions request)
    {
        var series = _series.GetSeries(id);
        if (request.AutomaticRenaming is not ("default" or "manual" or "automatic") ||
            request.SeasonTitles == null || request.SeasonTitles.Any(s => s.Value?.Length > 200 || !series.Seasons.Any(season => season.SeasonNumber == s.Key)))
        {
            return BadRequest(new { message = "Choose a valid naming option and season titles of at most 200 characters." });
        }

        lock (SettingsGate)
        {
            var options = _options.Read();
            options.Series[id] = request;
            _options.Save(options);
            return Ok(request);
        }
    }

    [HttpGet("searches")]
    public object GetSearches() => _searchActivity.Read();

    [HttpGet("storage")]
    public object GetStorage()
    {
        var downloads = _downloads.GetTrackedDownloads().Where(d => d.IsTrackable && d.State is not (TrackedDownloadState.Imported or TrackedDownloadState.Ignored or TrackedDownloadState.Failed)).ToList();
        return _roots.All().Select(root =>
        {
            var pending = downloads.Where(d => d.RemoteEpisode?.Series?.Path != null && IsInside(root.Path, d.RemoteEpisode.Series.Path))
                .DistinctBy(d => (d.DownloadClient, d.DownloadItem.DownloadId)).ToList();
            long? free = null;
            string? error = null;
            try
            {
                free = _disk.GetAvailableSpace(root.Path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                error = "Storage is unavailable or inaccessible.";
            }

            var copyEstimate = _options.Read().HardlinkOnly ? 0 : pending.Sum(d => Math.Max(0, d.DownloadItem.TotalSize));
            return new
            {
                path = root.Path, freeSpace = free, queuedDownloads = pending.Count,
                downloadRemaining = pending.Sum(d => Math.Max(0, d.DownloadItem.RemainingSize)),
                importSpaceEstimate = copyEstimate, mayRunOutOfSpace = free.HasValue && copyEstimate > free.Value,
                importMode = _options.Read().HardlinkOnly ? "Hardlink only; import fails if a link cannot be made." :
                    _config.CopyUsingHardlinks ? "Hardlink when possible; a full copy is needed if the source is on another filesystem or linking fails." : "Copy; a full second copy is needed while seeding.",
                explanation = "Conservative destination estimate: complete queued releases may be copied. Download-client space and compression temporary files are additional. Rows can share the same physical disk; do not add their free space together.",
                error
            };
        }).ToList();
    }

    [HttpPost("audit")]
    public object Audit(CancellationToken cancellationToken)
    {
        var issues = new List<object>();
        var seriesList = _series.GetAllSeries();
        var files = _files.GetFilesBySeriesIds(seriesList.Select(s => s.Id).ToList()).ToLookup(f => f.SeriesId);
        foreach (var series in seriesList)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_disk.FolderExists(series.Path))
            {
                issues.Add(new { seriesId = series.Id, title = series.Title, path = series.Path, reason = "Series folder is missing or its drive is offline." });
                continue;
            }

            var paths = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
            foreach (var file in files[series.Id])
            {
                var path = Path.Combine(series.Path, file.RelativePath);
                string? reason = null;
                if (!IsInside(series.Path, path))
                {
                    reason = "Registered file is outside its series folder.";
                }
                else if (!paths.Add(path))
                {
                    reason = "More than one file record uses this path.";
                }
                else if (!_disk.FileExists(path))
                {
                    reason = "Registered episode file is missing.";
                }

                if (reason != null)
                {
                    issues.Add(new { seriesId = series.Id, title = series.Title, path, reason });
                }
            }
        }

        return new { checkedAt = DateTime.UtcNow, seriesChecked = seriesList.Count, filesChecked = files.Sum(g => g.Count()), issues };
    }

    [HttpGet("keys")]
    public object GetKeys() => _options.Keys().Select(KeyResource).ToList();

    [HttpPost("keys")]
    public IActionResult CreateKey([FromBody] CompanionKeyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 80 || request.Permission is not ("read-library" or "add-series") || request.Days is < 1 or > 365)
        {
            return BadRequest(new { message = "Enter a name, choose a permission and an expiry between 1 and 365 days." });
        }

        var token = "spr_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var key = new ProAccessKey
        {
            Id = Guid.NewGuid().ToString("N"), Name = request.Name.Trim(), Permission = request.Permission,
            Created = DateTime.UtcNow, Expires = DateTime.UtcNow.AddDays(request.Days),
            Hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)))
        };
        lock (SettingsGate)
        {
            var keys = _options.Keys();
            if (keys.Count >= 50)
            {
                return BadRequest(new { message = "Revoke an unused key before creating another (limit 50)." });
            }

            keys.Add(key);
            _options.SaveKeys(keys);
        }

        return Ok(new { key = KeyResource(key), token });
    }

    [HttpDelete("keys/{id}")]
    public IActionResult RevokeKey(string id)
    {
        lock (SettingsGate)
        {
            var keys = _options.Keys();
            keys.RemoveAll(key => key.Id == id);
            _options.SaveKeys(keys);
        }

        return NoContent();
    }

    private static object KeyResource(ProAccessKey key) => new { key.Id, key.Name, key.Permission, key.Created, key.Expires };

    private static bool IsInside(string root, string path)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, comparison);
    }
}

public class CompanionKeyRequest
{
    public string Name { get; set; } = string.Empty;
    public string Permission { get; set; } = "read-library";
    public int Days { get; set; } = 90;
}

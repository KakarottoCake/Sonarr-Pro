using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.ImportLists.Exclusions;
using NzbDrone.Core.MediaCover;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.SeriesStats;
using Sonarr.Http;

namespace Sonarr.Api.V5.Series;

[V5ApiController("series/lookup")]
public class SeriesLookupController : Controller
{
    private readonly IMetadataProviderFactory _metadataProviderFactory;
    private readonly IBuildFileNames _fileNameBuilder;
    private readonly IMapCoversToLocal _coverMapper;
    private readonly IImportListExclusionService _importListExclusionService;

    public SeriesLookupController(IMetadataProviderFactory metadataProviderFactory, IBuildFileNames fileNameBuilder, IMapCoversToLocal coverMapper,  IImportListExclusionService importListExclusionService)
    {
        _metadataProviderFactory = metadataProviderFactory;
        _fileNameBuilder = fileNameBuilder;
        _coverMapper = coverMapper;
        _importListExclusionService = importListExclusionService;
    }

    /// <summary>
    /// Searches the given metadata source, defaulting to TheTVDB so existing callers are
    /// unaffected. The source chosen here becomes the one that owns any series added from
    /// the results, since a series is owned by exactly one provider.
    /// </summary>
    [HttpGet]
    public Ok<IEnumerable<SeriesResource>> Search([FromQuery] string term, [FromQuery] MetadataSourceType metadataSource = MetadataSourceType.Tvdb)
    {
        var results = _metadataProviderFactory.GetProvider(metadataSource).SearchForNewSeries(term);

        return TypedResults.Ok(MapToResource(results));
    }

    private IEnumerable<SeriesResource> MapToResource(IEnumerable<NzbDrone.Core.Tv.Series> series)
    {
        foreach (var currentSeries in series)
        {
            var resource = currentSeries.ToResource();

            _coverMapper.ConvertToLocalUrls(resource.Id, resource.Images, resource.Added);

            var poster = currentSeries.Images.FirstOrDefault(c => c.CoverType == MediaCoverTypes.Poster);

            if (poster != null)
            {
                resource.RemotePoster = poster.RemoteUrl;
            }

            resource.Folder = _fileNameBuilder.GetSeriesFolder(currentSeries);
            resource.Statistics = new SeriesStatistics().ToResource(resource.Seasons);
            resource.IsExcluded = _importListExclusionService.FindByTvdbId(currentSeries.TvdbId) is not null;

            yield return resource;
        }
    }
}

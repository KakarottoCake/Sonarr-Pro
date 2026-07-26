using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Core.MetadataSource.Imdb;
using NzbDrone.Core.MetadataSource.Tmdb;
using Sonarr.Http;

namespace Sonarr.Api.V5.Series;

/// <summary>
/// Lists the episode orderings a series can be added with.
/// <para>
/// An ordering is picked when a series is added and fixed thereafter, because season and
/// episode numbers are written into file and folder names.
/// </para>
/// </summary>
[V5ApiController("series/ordering")]
public class EpisodeOrderingController : Controller
{
    private readonly IProvideEpisodeOrderings _orderingProvider;
    private readonly IProvideImdbOrdering _imdbOrdering;

    public EpisodeOrderingController(IProvideEpisodeOrderings orderingProvider, IProvideImdbOrdering imdbOrdering)
    {
        _orderingProvider = orderingProvider;
        _imdbOrdering = imdbOrdering;
    }

    [HttpGet]
    public Ok<List<EpisodeOrderingResource>> GetOrderings([FromQuery] int tmdbId, [FromQuery] string? imdbId = null)
    {
        var orderings = new List<EpisodeOrderingResource>();

        if (tmdbId > 0)
        {
            // GetOrderings always yields the default ordering and swallows lookup failures,
            // so a missing or rejected API key degrades to "default only" rather than
            // erroring. The caller shows no choice when that is all it gets.
            orderings.AddRange(_orderingProvider.GetOrderings(tmdbId.ToString()).ToResource());
        }

        // Null unless the local IMDb index has been built and holds this series, so the
        // option only appears once it can actually be used.
        var imdb = _imdbOrdering.GetOrdering(imdbId);

        if (imdb != null)
        {
            orderings.Add(imdb.ToResource());
        }

        return TypedResults.Ok(orderings);
    }
}

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
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

    public EpisodeOrderingController(IProvideEpisodeOrderings orderingProvider)
    {
        _orderingProvider = orderingProvider;
    }

    [HttpGet]
    public Ok<List<EpisodeOrderingResource>> GetOrderings([FromQuery] int tmdbId)
    {
        if (tmdbId <= 0)
        {
            return TypedResults.Ok(new List<EpisodeOrderingResource>());
        }

        // GetOrderings always yields the default ordering and swallows lookup failures, so a
        // missing or rejected API key degrades to "default only" rather than erroring. The
        // caller shows no choice when that is all it gets.
        return TypedResults.Ok(_orderingProvider.GetOrderings(tmdbId.ToString()).ToResource());
    }
}

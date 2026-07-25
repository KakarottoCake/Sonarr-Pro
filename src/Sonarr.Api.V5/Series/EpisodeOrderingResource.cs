using NzbDrone.Core.MetadataSource.Ordering;

namespace Sonarr.Api.V5.Series;

public class EpisodeOrderingResource
{
    /// <summary>
    /// Provider-specific identifier, or null for the provider's default ordering.
    /// </summary>
    public string? Id { get; set; }

    public string? Name { get; set; }
    public string? Description { get; set; }
    public int EpisodeCount { get; set; }
    public int SeasonCount { get; set; }

    /// <summary>
    /// True when the ordering numbers every episode in one continuous run, which is how
    /// long-running anime is usually numbered and named by release groups.
    /// </summary>
    public bool IsAbsolute { get; set; }

    public bool IsDefault { get; set; }
}

public static class EpisodeOrderingResourceMapper
{
    public static EpisodeOrderingResource ToResource(this EpisodeOrdering model)
    {
        return new EpisodeOrderingResource
        {
            Id = model.Id,
            Name = model.Name,
            Description = model.Description,
            EpisodeCount = model.EpisodeCount,
            SeasonCount = model.SeasonCount,
            IsAbsolute = model.IsAbsolute,
            IsDefault = model.IsDefault
        };
    }

    public static List<EpisodeOrderingResource> ToResource(this IEnumerable<EpisodeOrdering> models)
    {
        return models.Select(ToResource).ToList();
    }
}

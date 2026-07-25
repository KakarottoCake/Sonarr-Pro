using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.Tmdb.Resource
{
    /// <summary>
    /// Response of /tv/{id}/episode_groups: the orderings published for a series.
    /// </summary>
    public class TmdbEpisodeGroupListResource
    {
        public List<TmdbEpisodeGroupSummaryResource> Results { get; set; }
    }

    public class TmdbEpisodeGroupSummaryResource
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }

        [JsonPropertyName("episode_count")]
        public int EpisodeCount { get; set; }

        [JsonPropertyName("group_count")]
        public int GroupCount { get; set; }

        /// <summary>
        /// See <see cref="TmdbEpisodeGroupType"/>. Type 2 is Absolute, which is the
        /// ordering long-running anime is usually watched in.
        /// </summary>
        public int Type { get; set; }
    }

    /// <summary>
    /// Response of /tv/episode_group/{id}: the ordering's actual contents.
    /// </summary>
    public class TmdbEpisodeGroupDetailResource
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public int Type { get; set; }
        public List<TmdbEpisodeGroupItemResource> Groups { get; set; }
    }

    public class TmdbEpisodeGroupItemResource
    {
        public string Id { get; set; }
        public string Name { get; set; }

        /// <summary>
        /// Position of this group within the ordering. Used as the season number.
        /// </summary>
        public int Order { get; set; }

        public List<TmdbEpisodeGroupEpisodeResource> Episodes { get; set; }
    }

    public class TmdbEpisodeGroupEpisodeResource
    {
        public int Id { get; set; }

        /// <summary>
        /// Position within the group. Zero-based, so the episode number is Order + 1.
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// The episode's number in the series' default ordering, not this group's.
        /// </summary>
        [JsonPropertyName("episode_number")]
        public int EpisodeNumber { get; set; }

        [JsonPropertyName("season_number")]
        public int SeasonNumber { get; set; }

        public string Name { get; set; }
        public string Overview { get; set; }

        [JsonPropertyName("air_date")]
        public string AirDate { get; set; }

        [JsonPropertyName("still_path")]
        public string StillPath { get; set; }
    }
}

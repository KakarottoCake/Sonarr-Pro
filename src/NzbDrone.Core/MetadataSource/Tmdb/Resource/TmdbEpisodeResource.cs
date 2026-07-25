using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.Tmdb.Resource
{
    public class TmdbSeasonDetailResource
    {
        [JsonPropertyName("season_number")]
        public int SeasonNumber { get; set; }

        public List<TmdbEpisodeResource> Episodes { get; set; }
    }

    public class TmdbEpisodeResource
    {
        public int Id { get; set; }

        [JsonPropertyName("episode_number")]
        public int EpisodeNumber { get; set; }

        [JsonPropertyName("season_number")]
        public int SeasonNumber { get; set; }

        public string Name { get; set; }
        public string Overview { get; set; }

        [JsonPropertyName("air_date")]
        public string AirDate { get; set; }

        public int? Runtime { get; set; }

        [JsonPropertyName("still_path")]
        public string StillPath { get; set; }

        [JsonPropertyName("vote_average")]
        public decimal VoteAverage { get; set; }

        [JsonPropertyName("vote_count")]
        public int VoteCount { get; set; }
    }
}

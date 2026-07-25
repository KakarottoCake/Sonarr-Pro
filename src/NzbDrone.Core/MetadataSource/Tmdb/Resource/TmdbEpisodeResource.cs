using System.Collections.Generic;
using Newtonsoft.Json;

namespace NzbDrone.Core.MetadataSource.Tmdb.Resource
{
    public class TmdbSeasonDetailResource
    {
        [JsonProperty("season_number")]
        public int SeasonNumber { get; set; }

        public List<TmdbEpisodeResource> Episodes { get; set; }
    }

    public class TmdbEpisodeResource
    {
        public int Id { get; set; }

        [JsonProperty("episode_number")]
        public int EpisodeNumber { get; set; }

        [JsonProperty("season_number")]
        public int SeasonNumber { get; set; }

        public string Name { get; set; }
        public string Overview { get; set; }

        [JsonProperty("air_date")]
        public string AirDate { get; set; }

        public int? Runtime { get; set; }

        [JsonProperty("still_path")]
        public string StillPath { get; set; }

        [JsonProperty("vote_average")]
        public decimal VoteAverage { get; set; }

        [JsonProperty("vote_count")]
        public int VoteCount { get; set; }
    }
}

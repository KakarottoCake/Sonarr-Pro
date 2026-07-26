using System.Collections.Generic;
using Newtonsoft.Json;

namespace NzbDrone.Core.MetadataSource.Tmdb.Resource
{
    public class TmdbSeriesResource
    {
        public int Id { get; set; }
        public string Name { get; set; }

        [JsonProperty("original_name")]
        public string OriginalName { get; set; }

        public string Overview { get; set; }

        [JsonProperty("first_air_date")]
        public string FirstAirDate { get; set; }

        [JsonProperty("last_air_date")]
        public string LastAirDate { get; set; }

        public string Status { get; set; }

        [JsonProperty("episode_run_time")]
        public List<int> EpisodeRunTime { get; set; }

        [JsonProperty("original_language")]
        public string OriginalLanguage { get; set; }

        [JsonProperty("origin_country")]
        public List<string> OriginCountry { get; set; }

        public List<TmdbGenreResource> Genres { get; set; }

        [JsonProperty("vote_average")]
        public decimal VoteAverage { get; set; }

        [JsonProperty("vote_count")]
        public int VoteCount { get; set; }

        [JsonProperty("poster_path")]
        public string PosterPath { get; set; }

        [JsonProperty("backdrop_path")]
        public string BackdropPath { get; set; }

        public List<TmdbNetworkResource> Networks { get; set; }
        public List<TmdbSeasonResource> Seasons { get; set; }

        /// <summary>
        /// Populated via append_to_response so the ids arrive on the same request.
        /// </summary>
        [JsonProperty("external_ids")]
        public TmdbExternalIdsResource ExternalIds { get; set; }

        [JsonProperty("content_ratings")]
        public TmdbContentRatingsResource ContentRatings { get; set; }
    }

    public class TmdbSearchResponse
    {
        public List<TmdbSeriesResource> Results { get; set; }
    }

    public class TmdbGenreResource
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class TmdbNetworkResource
    {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    public class TmdbSeasonResource
    {
        public int Id { get; set; }

        [JsonProperty("season_number")]
        public int SeasonNumber { get; set; }

        [JsonProperty("episode_count")]
        public int EpisodeCount { get; set; }

        public string Name { get; set; }

        [JsonProperty("poster_path")]
        public string PosterPath { get; set; }
    }

    public class TmdbExternalIdsResource
    {
        [JsonProperty("imdb_id")]
        public string ImdbId { get; set; }

        [JsonProperty("tvdb_id")]
        public int? TvdbId { get; set; }
    }

    public class TmdbContentRatingsResource
    {
        public List<TmdbContentRatingResource> Results { get; set; }
    }

    public class TmdbContentRatingResource
    {
        [JsonProperty("iso_3166_1")]
        public string Country { get; set; }

        public string Rating { get; set; }
    }
}

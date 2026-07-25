using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NzbDrone.Core.MetadataSource.Tmdb.Resource
{
    public class TmdbSeriesResource
    {
        public int Id { get; set; }
        public string Name { get; set; }

        [JsonPropertyName("original_name")]
        public string OriginalName { get; set; }

        public string Overview { get; set; }

        [JsonPropertyName("first_air_date")]
        public string FirstAirDate { get; set; }

        [JsonPropertyName("last_air_date")]
        public string LastAirDate { get; set; }

        public string Status { get; set; }

        [JsonPropertyName("episode_run_time")]
        public List<int> EpisodeRunTime { get; set; }

        [JsonPropertyName("original_language")]
        public string OriginalLanguage { get; set; }

        [JsonPropertyName("origin_country")]
        public List<string> OriginCountry { get; set; }

        public List<TmdbGenreResource> Genres { get; set; }

        [JsonPropertyName("vote_average")]
        public decimal VoteAverage { get; set; }

        [JsonPropertyName("vote_count")]
        public int VoteCount { get; set; }

        [JsonPropertyName("poster_path")]
        public string PosterPath { get; set; }

        [JsonPropertyName("backdrop_path")]
        public string BackdropPath { get; set; }

        public List<TmdbNetworkResource> Networks { get; set; }
        public List<TmdbSeasonResource> Seasons { get; set; }

        /// <summary>
        /// Populated via append_to_response so the ids arrive on the same request.
        /// </summary>
        [JsonPropertyName("external_ids")]
        public TmdbExternalIdsResource ExternalIds { get; set; }

        [JsonPropertyName("content_ratings")]
        public TmdbContentRatingsResource ContentRatings { get; set; }
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

        [JsonPropertyName("season_number")]
        public int SeasonNumber { get; set; }

        [JsonPropertyName("episode_count")]
        public int EpisodeCount { get; set; }

        public string Name { get; set; }

        [JsonPropertyName("poster_path")]
        public string PosterPath { get; set; }
    }

    public class TmdbExternalIdsResource
    {
        [JsonPropertyName("imdb_id")]
        public string ImdbId { get; set; }

        [JsonPropertyName("tvdb_id")]
        public int? TvdbId { get; set; }
    }

    public class TmdbContentRatingsResource
    {
        public List<TmdbContentRatingResource> Results { get; set; }
    }

    public class TmdbContentRatingResource
    {
        [JsonPropertyName("iso_3166_1")]
        public string Country { get; set; }

        public string Rating { get; set; }
    }
}

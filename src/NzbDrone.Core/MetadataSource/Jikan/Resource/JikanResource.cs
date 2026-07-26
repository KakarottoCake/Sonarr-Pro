using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace NzbDrone.Core.MetadataSource.Jikan.Resource
{
    public class JikanAnimeResponse
    {
        public JikanAnimeResource Data { get; set; }
    }

    public class JikanSearchResponse
    {
        public List<JikanAnimeResource> Data { get; set; }
    }

    public class JikanAnimeResource
    {
        [JsonProperty("mal_id")]
        public int MalId { get; set; }

        public string Title { get; set; }

        [JsonProperty("title_english")]
        public string TitleEnglish { get; set; }

        [JsonProperty("title_japanese")]
        public string TitleJapanese { get; set; }

        public List<JikanTitleResource> Titles { get; set; }
        public string Synopsis { get; set; }
        public int? Episodes { get; set; }
        public string Status { get; set; }
        public JikanAiredResource Aired { get; set; }
        public string Duration { get; set; }
        public decimal? Score { get; set; }

        [JsonProperty("scored_by")]
        public int? ScoredBy { get; set; }

        public JikanImagesResource Images { get; set; }
        public List<JikanNamedResource> Genres { get; set; }
        public List<JikanNamedResource> Studios { get; set; }
        public string Rating { get; set; }
    }

    public class JikanTitleResource
    {
        public string Type { get; set; }
        public string Title { get; set; }
    }

    public class JikanAiredResource
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
    }

    public class JikanImagesResource
    {
        public JikanImageSetResource Jpg { get; set; }
    }

    public class JikanImageSetResource
    {
        [JsonProperty("large_image_url")]
        public string LargeImageUrl { get; set; }

        [JsonProperty("image_url")]
        public string ImageUrl { get; set; }
    }

    public class JikanNamedResource
    {
        public string Name { get; set; }
    }

    public class JikanEpisodeListResponse
    {
        public List<JikanEpisodeResource> Data { get; set; }
        public JikanPaginationResource Pagination { get; set; }
    }

    public class JikanPaginationResource
    {
        [JsonProperty("last_visible_page")]
        public int LastVisiblePage { get; set; }

        [JsonProperty("has_next_page")]
        public bool HasNextPage { get; set; }
    }

    public class JikanEpisodeResource
    {
        [JsonProperty("mal_id")]
        public int MalId { get; set; }

        public string Title { get; set; }

        [JsonProperty("title_japanese")]
        public string TitleJapanese { get; set; }

        public DateTime? Aired { get; set; }

        /// <summary>
        /// Filler and recap episodes are aired and numbered, so they stay in the list;
        /// the flags are kept so the UI can mark them.
        /// </summary>
        public bool Filler { get; set; }

        public bool Recap { get; set; }
    }
}

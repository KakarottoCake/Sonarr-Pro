using System.Collections.Generic;

namespace NzbDrone.Core.MetadataSource.AniList.Resource
{
    public class AniListMediaResponse
    {
        public AniListMediaData Data { get; set; }
    }

    public class AniListMediaData
    {
        public AniListMediaResource Media { get; set; }
    }

    public class AniListSearchResponse
    {
        public AniListSearchData Data { get; set; }
    }

    public class AniListSearchData
    {
        public AniListSearchPage Page { get; set; }
    }

    public class AniListSearchPage
    {
        public List<AniListMediaResource> Media { get; set; }
    }

    public class AniListMediaResource
    {
        public int Id { get; set; }
        public int? IdMal { get; set; }
        public AniListTitleResource Title { get; set; }

        /// <summary>
        /// Alternative names, including fan abbreviations and per-region titles. These are
        /// what release groups actually put in filenames, so they matter more for matching
        /// than the canonical title does.
        /// </summary>
        public List<string> Synonyms { get; set; }

        public string Description { get; set; }
        public string Format { get; set; }
        public string Status { get; set; }

        /// <summary>
        /// Total episode count. Null while a series is still airing.
        /// </summary>
        public int? Episodes { get; set; }

        public int? Duration { get; set; }
        public AniListDateResource StartDate { get; set; }
        public AniListDateResource EndDate { get; set; }
        public string CountryOfOrigin { get; set; }
        public List<string> Genres { get; set; }
        public int? AverageScore { get; set; }
        public int? Popularity { get; set; }
        public AniListCoverImageResource CoverImage { get; set; }
        public string BannerImage { get; set; }
        public AniListStudioConnection Studios { get; set; }
        public AniListAiringScheduleConnection AiringSchedule { get; set; }
    }

    public class AniListTitleResource
    {
        public string Romaji { get; set; }
        public string English { get; set; }
        public string Native { get; set; }
        public string UserPreferred { get; set; }
    }

    public class AniListDateResource
    {
        public int? Year { get; set; }
        public int? Month { get; set; }
        public int? Day { get; set; }
    }

    public class AniListCoverImageResource
    {
        public string ExtraLarge { get; set; }
        public string Large { get; set; }
    }

    public class AniListStudioConnection
    {
        public List<AniListStudioResource> Nodes { get; set; }
    }

    public class AniListStudioResource
    {
        public string Name { get; set; }
        public bool IsAnimationStudio { get; set; }
    }

    public class AniListAiringScheduleConnection
    {
        public List<AniListAiringScheduleResource> Nodes { get; set; }
    }

    public class AniListAiringScheduleResource
    {
        public int Episode { get; set; }

        /// <summary>
        /// Unix timestamp of the broadcast.
        /// </summary>
        public long AiringAt { get; set; }
    }
}

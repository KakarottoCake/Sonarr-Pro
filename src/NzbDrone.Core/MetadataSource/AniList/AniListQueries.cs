namespace NzbDrone.Core.MetadataSource.AniList
{
    public static class AniListQueries
    {
        public const string BaseUrl = "https://graphql.anilist.co";

        /// <summary>
        /// Full detail for one anime. perPage on airingSchedule is capped at AniList's
        /// maximum of 50 per page; long-running series need paging, which
        /// <see cref="AniListProxy"/> handles.
        /// </summary>
        public const string MediaById = @"
            query ($id: Int, $page: Int) {
                Media(id: $id, type: ANIME) {
                    id
                    idMal
                    title { romaji english native userPreferred }
                    synonyms
                    description(asHtml: false)
                    format
                    status
                    episodes
                    duration
                    startDate { year month day }
                    endDate { year month day }
                    countryOfOrigin
                    genres
                    averageScore
                    popularity
                    coverImage { extraLarge large }
                    bannerImage
                    studios { nodes { name isAnimationStudio } }
                    airingSchedule(page: $page, perPage: 50) {
                        nodes { episode airingAt }
                    }
                }
            }
        ";

        /// <summary>
        /// Title search, used when adding a series.
        /// </summary>
        public const string SearchByTitle = @"
            query ($search: String) {
                Page(page: 1, perPage: 20) {
                    media(search: $search, type: ANIME) {
                        id
                        idMal
                        title { romaji english native userPreferred }
                        synonyms
                        description(asHtml: false)
                        format
                        status
                        episodes
                        duration
                        startDate { year month day }
                        endDate { year month day }
                        countryOfOrigin
                        genres
                        averageScore
                        popularity
                        coverImage { extraLarge large }
                        bannerImage
                    }
                }
            }
        ";
    }
}

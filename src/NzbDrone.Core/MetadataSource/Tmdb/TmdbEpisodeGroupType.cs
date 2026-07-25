namespace NzbDrone.Core.MetadataSource.Tmdb
{
    /// <summary>
    /// TMDB's episode group types, as published by the API.
    /// </summary>
    public enum TmdbEpisodeGroupType
    {
        OriginalAirDate = 1,

        /// <summary>
        /// One continuous run of episode numbers with no seasons. This is how long-running
        /// anime such as One Piece is usually numbered and discussed, and what release
        /// groups put in filenames.
        /// </summary>
        Absolute = 2,

        Dvd = 3,
        Digital = 4,
        StoryArc = 5,
        Production = 6,
        Tv = 7
    }
}

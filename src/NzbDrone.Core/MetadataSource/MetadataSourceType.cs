namespace NzbDrone.Core.MetadataSource
{
    /// <summary>
    /// Identifies which provider owns a series' metadata.
    /// Exactly one provider owns a given series: providers are selected, not merged.
    /// Merging structural data between providers causes episode rows to churn on every
    /// refresh, which detaches episode files and triggers re-downloads of owned content.
    /// </summary>
    public enum MetadataSourceType
    {
        /// <summary>
        /// TheTVDB, via the SkyHook proxy. The default, and the only source before this fork.
        /// </summary>
        Tvdb = 0,

        Tmdb = 1,

        AniList = 2,

        MyAnimeList = 3
    }
}

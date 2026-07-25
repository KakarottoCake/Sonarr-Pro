namespace NzbDrone.Core.MetadataSource.Ordering
{
    /// <summary>
    /// An episode numbering scheme a series can be added with, such as TMDB's
    /// "Absolute" episode group. Offered when adding a series and fixed thereafter:
    /// season and episode numbers are written into file and folder names, so changing
    /// the ordering of an existing series would mean renaming its library on disk.
    /// </summary>
    public class EpisodeOrdering
    {
        /// <summary>
        /// Provider-specific identifier. Null means the provider's default ordering.
        /// </summary>
        public string Id { get; set; }

        public string Name { get; set; }
        public string Description { get; set; }
        public int EpisodeCount { get; set; }
        public int SeasonCount { get; set; }

        /// <summary>
        /// True when this ordering numbers every episode in one continuous run.
        /// </summary>
        public bool IsAbsolute { get; set; }

        public bool IsDefault => Id == null;

        public override string ToString()
        {
            return string.Format("[{0}] {1}", Id ?? "default", Name);
        }
    }
}

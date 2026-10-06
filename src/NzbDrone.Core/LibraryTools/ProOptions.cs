using System;
using System.Collections.Generic;

namespace NzbDrone.Core.LibraryTools
{
    public class ProOptions
    {
        public bool AutomationPaused { get; set; }
        public bool HardlinkOnly { get; set; }
        public bool MatchExternalIds { get; set; }
        public bool PreferAnimeSeasonPacks { get; set; }
        public Dictionary<int, ProSeriesOptions> Series { get; set; } = new();
    }

    public class ProSeriesOptions
    {
        public bool KeepVersions { get; set; }
        public string AutomaticRenaming { get; set; } = "default";
        public Dictionary<int, string> SeasonTitles { get; set; } = new();
    }

    public class ProAccessKey
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Hash { get; set; }
        public string Permission { get; set; }
        public DateTime Created { get; set; }
        public DateTime? Expires { get; set; }
    }
}

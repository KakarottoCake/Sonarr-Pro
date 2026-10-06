using System;
using NzbDrone.Core.Datastore;

namespace NzbDrone.Core.LibraryTools
{
    public class RetainedVersion : ModelBase
    {
        public int SeriesId { get; set; }
        public string RelativePath { get; set; }
        public string MetadataJson { get; set; }
        public string EpisodeIdsJson { get; set; }
        public DateTime Created { get; set; }
    }
}

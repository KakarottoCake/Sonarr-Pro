using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.LibraryTools
{
    public class RestoreRecycledFileCommand : Command
    {
        public string RelativePath { get; set; }
        public int SeriesId { get; set; }
        public int SeasonNumber { get; set; }
        public override bool RequiresDiskAccess => true;
        public override bool SendUpdatesToClient => true;
        public override string CompletionMessage => "File restored. A library scan is queued to match its episodes.";
    }
}

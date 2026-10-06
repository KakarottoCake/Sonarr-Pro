using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.LibraryTools
{
    public class ActivateRetainedVersionCommand : Command
    {
        public int SeriesId { get; set; }
        public int VersionId { get; set; }
        public override bool RequiresDiskAccess => true;
        public override bool SendUpdatesToClient => true;
        public override string CompletionMessage => "Version activated. The previous active file was retained.";
    }
}

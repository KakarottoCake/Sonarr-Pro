using NzbDrone.Core.Download.FakeRelease;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Exceptions
{
    /// <summary>
    /// Thrown when a release's contents identify it as fake before it reaches the download client.
    /// Derives from <see cref="ReleaseBlockedException"/> so the grab is abandoned without
    /// recording a failure against the indexer - the indexer is not at fault, the release is.
    /// </summary>
    public class FakeReleaseException : ReleaseBlockedException
    {
        public FakeReleaseRejectionReason Reason { get; }

        public FakeReleaseException(ReleaseInfo release, FakeReleaseRejectionReason reason, string message)
            : base(release, message)
        {
            Reason = reason;
        }
    }
}

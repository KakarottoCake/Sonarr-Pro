namespace NzbDrone.Core.Download.FakeRelease
{
    public class FakeReleaseDetection
    {
        private FakeReleaseDetection()
        {
        }

        public bool IsFake { get; private set; }
        public FakeReleaseRejectionReason Reason { get; private set; }
        public string Message { get; private set; }

        /// <summary>
        /// The file that triggered the detection, if the reason relates to a specific file.
        /// </summary>
        public string OffendingFile { get; private set; }

        public static FakeReleaseDetection Clean()
        {
            return new FakeReleaseDetection { IsFake = false };
        }

        public static FakeReleaseDetection Fake(FakeReleaseRejectionReason reason, string message, string offendingFile = null)
        {
            return new FakeReleaseDetection
            {
                IsFake = true,
                Reason = reason,
                Message = message,
                OffendingFile = offendingFile
            };
        }
    }
}

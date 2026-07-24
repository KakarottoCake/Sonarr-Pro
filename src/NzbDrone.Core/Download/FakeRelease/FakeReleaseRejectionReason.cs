namespace NzbDrone.Core.Download.FakeRelease
{
    public enum FakeReleaseRejectionReason
    {
        /// <summary>
        /// A media file name is immediately followed by an executable extension, e.g. "Episode.1080p.mkv.exe".
        /// </summary>
        DisguisedExecutable,

        /// <summary>
        /// The torrent contains an executable, installer or script payload.
        /// </summary>
        ExecutablePayload,

        /// <summary>
        /// The torrent contains neither video content nor archives, so there is nothing usable to import.
        /// </summary>
        NoUsableContent,

        /// <summary>
        /// The torrent contains archives but no video, alongside a file advertising a password.
        /// </summary>
        PasswordBait
    }
}

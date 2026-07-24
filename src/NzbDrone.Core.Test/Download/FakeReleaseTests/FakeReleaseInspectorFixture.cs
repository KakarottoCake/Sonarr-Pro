using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Download.FakeRelease;
using NzbDrone.Core.MediaFiles.TorrentInfo;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download.FakeReleaseTests
{
    [TestFixture]
    public class FakeReleaseInspectorFixture : CoreTest<FakeReleaseInspector>
    {
        private const long VideoSize = 1024L * 1024L * 1024L;
        private const long SmallSize = 1024L * 512L;

        private static TorrentContents Contents(params TorrentContentFile[] files)
        {
            return new TorrentContents
            {
                Name = "Some.Show.S01E01.1080p.WEB-DL",
                Files = new List<TorrentContentFile>(files)
            };
        }

        private static TorrentContentFile File(string path, long length)
        {
            return new TorrentContentFile(path, length);
        }

        [Test]
        public void should_accept_a_normal_single_episode_release()
        {
            var result = Subject.Inspect(Contents(
                File("Some.Show.S01E01.1080p.WEB-DL.mkv", VideoSize),
                File("Some.Show.S01E01.1080p.WEB-DL.nfo", 2048),
                File("Sample/sample.mkv", SmallSize)));

            result.IsFake.Should().BeFalse();
        }

        [Test]
        public void should_accept_a_season_pack()
        {
            var result = Subject.Inspect(Contents(
                File("Season 1/Show.S01E01.mkv", VideoSize),
                File("Season 1/Show.S01E02.mkv", VideoSize),
                File("Season 1/Show.S01E03.mkv", VideoSize)));

            result.IsFake.Should().BeFalse();
        }

        [Test]
        public void should_accept_a_legitimate_rar_packed_release()
        {
            // Scene releases are commonly shipped as split RAR sets with no loose video file.
            // Their contents cannot be inspected, so they must be given the benefit of the doubt.
            var result = Subject.Inspect(Contents(
                File("show.s01e01.rar", VideoSize),
                File("show.s01e01.r00", VideoSize),
                File("show.s01e01.r01", VideoSize),
                File("show.s01e01.nfo", 2048)));

            result.IsFake.Should().BeFalse();
        }

        [Test]
        public void should_reject_executable_disguised_as_video()
        {
            var result = Subject.Inspect(Contents(
                File("Some.Show.S01E01.1080p.mkv.exe", 4096000)));

            result.IsFake.Should().BeTrue();
            result.Reason.Should().Be(FakeReleaseRejectionReason.DisguisedExecutable);
        }

        [Test]
        public void should_reject_executable_alongside_real_video()
        {
            var result = Subject.Inspect(Contents(
                File("Some.Show.S01E01.mkv", VideoSize),
                File("Setup.exe", 4096000)));

            result.IsFake.Should().BeTrue();
            result.Reason.Should().Be(FakeReleaseRejectionReason.ExecutablePayload);
        }

        [TestCase("install.msi")]
        [TestCase("player.scr")]
        [TestCase("run.bat")]
        [TestCase("codec.vbs")]
        [TestCase("watch.lnk")]
        [TestCase("click.url")]
        [TestCase("payload.apk")]
        public void should_reject_known_executable_extensions(string fileName)
        {
            var result = Subject.Inspect(Contents(
                File("Some.Show.S01E01.mkv", VideoSize),
                File(fileName, 40960)));

            result.IsFake.Should().BeTrue();
            result.Reason.Should().Be(FakeReleaseRejectionReason.ExecutablePayload);
        }

        [Test]
        public void should_reject_password_bait_when_no_video_present()
        {
            var result = Subject.Inspect(Contents(
                File("Show.S01E01.part1.rar", VideoSize),
                File("Show.S01E01.part2.rar", VideoSize),
                File("PASSWORD.txt", 128)));

            result.IsFake.Should().BeTrue();
            result.Reason.Should().Be(FakeReleaseRejectionReason.PasswordBait);
        }

        [Test]
        public void should_not_treat_password_named_file_as_bait_when_video_is_present()
        {
            var result = Subject.Inspect(Contents(
                File("Show.S01E01.mkv", VideoSize),
                File("password.txt", 128)));

            result.IsFake.Should().BeFalse();
        }

        [Test]
        public void should_reject_release_with_no_video_and_no_archive()
        {
            var result = Subject.Inspect(Contents(
                File("readme.txt", 512),
                File("poster.jpg", 40960)));

            result.IsFake.Should().BeTrue();
            result.Reason.Should().Be(FakeReleaseRejectionReason.NoUsableContent);
        }

        [Test]
        public void should_reject_when_only_a_tiny_video_is_present()
        {
            var result = Subject.Inspect(Contents(
                File("Some.Show.S01E01.mkv", SmallSize)));

            result.IsFake.Should().BeTrue();
            result.Reason.Should().Be(FakeReleaseRejectionReason.NoUsableContent);
        }

        [Test]
        public void should_ignore_bep47_padding_files()
        {
            var result = Subject.Inspect(Contents(
                File("Some.Show.S01E01.mkv", VideoSize),
                File(".pad/12345", 12345),
                File("_____padding_file_0", 4096)));

            result.IsFake.Should().BeFalse();
        }

        [Test]
        public void should_accept_when_torrent_has_no_files()
        {
            // Nothing to judge - never block on absence of information.
            var result = Subject.Inspect(Contents());

            result.IsFake.Should().BeFalse();
        }

        [Test]
        public void should_accept_null_contents()
        {
            var result = Subject.Inspect(null);

            result.IsFake.Should().BeFalse();
        }

        [Test]
        public void should_report_the_offending_file()
        {
            var result = Subject.Inspect(Contents(
                File("Some.Show.S01E01.mkv", VideoSize),
                File("extras/Setup.exe", 4096000)));

            result.IsFake.Should().BeTrue();
            result.OffendingFile.Should().Be("extras/Setup.exe");
        }
    }
}

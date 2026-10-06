using System;
using System.IO;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.LibraryTools;

namespace NzbDrone.Core.Test.LibraryTools
{
    [TestFixture]
    public class ArchivePathGuardFixture
    {
        [TestCase("../outside.mkv")]
        [TestCase("folder/../../outside.mkv")]
        [TestCase("")]
        public void rejects_paths_outside_the_configured_folder(string relative)
        {
            Assert.Throws<IOException>(() => ArchivePathGuard.Resolve(Path.Combine(Path.GetTempPath(), "archive-guard"), relative));
        }

        [Test]
        public void accepts_nested_relative_media_paths()
        {
            var root = Path.Combine(Path.GetTempPath(), "archive-guard-" + Guid.NewGuid().ToString("N"));
            ArchivePathGuard.Resolve(root, Path.Combine("Season 01", "episode.mkv")).Should().Be(Path.Combine(root, "Season 01", "episode.mkv"));
        }
    }
}

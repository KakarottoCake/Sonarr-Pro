using System;
using System.Collections.Generic;
using System.IO;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.LibraryTools;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.LibraryTools
{
    [TestFixture]
    public class RecycleRestoreFixture : CoreTest<RecycleRestoreService>
    {
        private Series _series;
        private string _recycle;
        private string _target;

        [SetUp]
        public void Setup()
        {
            var root = Path.Combine(Path.GetTempPath(), "restore-fixture-" + Guid.NewGuid().ToString("N"));
            _recycle = Path.Combine(root, "recycle");
            _series = new Series { Id = 7, Path = Path.Combine(root, "show"), Seasons = new List<Season> { new Season { SeasonNumber = 1 } } };
            _target = Path.Combine(_series.Path, "episode.mkv");
            Mocker.GetMock<IConfigService>().Setup(c => c.RecycleBin).Returns(_recycle);
            Mocker.GetMock<ISeriesService>().Setup(s => s.GetSeries(7)).Returns(_series);
            Mocker.GetMock<IDiskProvider>().Setup(d => d.FolderExists(_series.Path)).Returns(true);
            Mocker.GetMock<IDiskProvider>().Setup(d => d.FileExists(Path.Combine(_recycle, "episode.mkv"))).Returns(true);
        }

        [Test]
        public void restore_does_not_overwrite_an_existing_file()
        {
            Mocker.GetMock<IDiskProvider>().Setup(d => d.FileExists(_target)).Returns(true);
            Assert.Throws<IOException>(() => Subject.Execute(new RestoreRecycledFileCommand { SeriesId = 7, SeasonNumber = 1, RelativePath = "episode.mkv" }));
            Mocker.GetMock<IDiskTransferService>().Verify(d => d.TransferFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TransferMode>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void restore_does_not_change_the_shared_video_extensions()
        {
            var original = new HashSet<string>(MediaFileExtensions.Extensions);
            Subject.Execute(new RestoreRecycledFileCommand { SeriesId = 7, SeasonNumber = 1, RelativePath = "episode.mkv" });
            MediaFileExtensions.Extensions.Should().BeEquivalentTo(original);
            Mocker.GetMock<IDiskTransferService>().Verify(d => d.TransferFile(Path.Combine(_recycle, "episode.mkv"), _target, TransferMode.Move, false), Times.Once());
        }

        [Test]
        public void restore_waits_for_series_moves()
        {
            _series.PendingPath = Path.Combine(Path.GetTempPath(), "pending");
            Assert.Throws<IOException>(() => Subject.Execute(new RestoreRecycledFileCommand { SeriesId = 7, SeasonNumber = 1, RelativePath = "episode.mkv" }));
            Mocker.GetMock<IDiskTransferService>().Verify(d => d.TransferFile(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TransferMode>(), It.IsAny<bool>()), Times.Never());
        }
    }
}

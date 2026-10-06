using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.History;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.LibraryTools
{
    [TestFixture]
    public class StalledRecoveryFixture : CoreTest<FailedDownloadService>
    {
        [TestCase(DownloadItemStatus.Downloading, true)]
        [TestCase(DownloadItemStatus.Warning, true)]
        [TestCase(DownloadItemStatus.Paused, false)]
        [TestCase(DownloadItemStatus.Queued, false)]
        [TestCase(DownloadItemStatus.Completed, false)]
        public void only_recovers_downloads_expected_to_make_progress(DownloadItemStatus status, bool expected)
        {
            Mocker.GetMock<IConfigService>().SetupGet(c => c.StalledTorrentTimeout).Returns(30);
            Mocker.GetMock<IHistoryService>().Setup(h => h.Find("torrent", EpisodeHistoryEventType.Grabbed)).Returns(new List<EpisodeHistory> { new EpisodeHistory() });
            var download = new TrackedDownload
            {
                Protocol = DownloadProtocol.Torrent,
                LastProgressDate = DateTime.UtcNow.AddMinutes(-31),
                State = TrackedDownloadState.Downloading,
                DownloadItem = new DownloadClientItem { DownloadId = "torrent", Status = status }
            };
            Subject.Check(download);
            (download.State == TrackedDownloadState.FailedPending).Should().Be(expected);
        }

        [Test]
        public void does_not_fail_foreign_downloads_or_recent_progress()
        {
            Mocker.GetMock<IConfigService>().SetupGet(c => c.StalledTorrentTimeout).Returns(30);
            Mocker.GetMock<IHistoryService>().Setup(h => h.Find("torrent", EpisodeHistoryEventType.Grabbed)).Returns(new List<EpisodeHistory>());
            var download = new TrackedDownload
            {
                Protocol = DownloadProtocol.Torrent,
                LastProgressDate = DateTime.UtcNow.AddDays(-1),
                State = TrackedDownloadState.Downloading,
                DownloadItem = new DownloadClientItem { DownloadId = "torrent", Status = DownloadItemStatus.Downloading }
            };
            Subject.Check(download);
            download.State.Should().Be(TrackedDownloadState.Downloading);
            download.LastProgressDate = DateTime.UtcNow;
            Subject.Check(download);
            download.State.Should().Be(TrackedDownloadState.Downloading);
        }
    }
}

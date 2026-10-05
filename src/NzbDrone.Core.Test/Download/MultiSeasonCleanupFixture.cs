using System.Collections.Generic;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Download
{
    [TestFixture]
    public class MultiSeasonCleanupFixture : CoreTest<DownloadProcessingService>
    {
        [TestCase("Series.Title.S01-S03", false)]
        [TestCase("Series.Title.S03", true)]
        public void should_keep_multi_season_packs_when_the_client_reports_an_imported_download_as_removable(string title, bool removed)
        {
            Mocker.GetMock<ITrackedDownloadService>().Setup(service => service.GetTrackedDownloads()).Returns(new List<TrackedDownload>
            {
                new TrackedDownload
                {
                    State = TrackedDownloadState.Imported,
                    IsTrackable = false,
                    DownloadItem = new DownloadClientItem { Title = title, CanBeRemoved = true }
                }
            });

            Subject.Execute(new ProcessMonitoredDownloadsCommand());

            Mocker.GetMock<IEventAggregator>().Verify(events => events.PublishEvent(It.IsAny<DownloadCanBeRemovedEvent>()), removed ? Times.Once() : Times.Never());
        }
    }
}

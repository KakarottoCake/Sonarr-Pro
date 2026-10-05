using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.MediaFiles.EpisodeImport.Manual;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class ImportedDownloadPreviewFixture : CoreTest<ManualImportService>
    {
        [Test]
        public void should_use_the_client_path_for_an_imported_download_without_a_prepared_import_item()
        {
            var path = @"C:\downloads\Series.Title.S01-S03".AsOsAgnostic();
            Mocker.GetMock<ITrackedDownloadService>().Setup(service => service.Find("pack")).Returns(new TrackedDownload
            {
                State = TrackedDownloadState.Imported,
                DownloadItem = new DownloadClientItem { OutputPath = new OsPath(path) }
            });

            Subject.GetMediaFiles(null, "pack", null, true).Should().BeEmpty();

            Mocker.GetMock<IDiskProvider>().Verify(provider => provider.FolderExists(path), Times.Once());
        }
    }
}

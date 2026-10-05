using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Download;
using NzbDrone.Core.History;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.EpisodeImport;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles
{
    [TestFixture]
    public class MultiSeasonImportFixture : CoreTest<DownloadedEpisodesImportService>
    {
        private string _path;
        private DownloadClientItem _download;
        private Series _series;
        private List<ImportDecision> _decisions;

        [SetUp]
        public void Setup()
        {
            _path = @"C:\downloads\Series.Title.S01-S03".AsOsAgnostic();
            _series = new Series { Id = 1 };
            _download = new DownloadClientItem { DownloadId = "pack", Title = "Series.Title.S01-S03", CanMoveFiles = true };
            _decisions = new List<ImportDecision>
            {
                new ImportDecision(new LocalEpisode { Path = _path + "/Series.Title.S01E01.mkv", Episodes = new List<Episode> { new Episode { Id = 101, SeasonNumber = 1 } } }),
                new ImportDecision(new LocalEpisode { Path = _path + "/Series.Title.S03E01.mkv", Episodes = new List<Episode> { new Episode { Id = 301, SeasonNumber = 3 } } })
            };
            Mocker.GetMock<IDiskProvider>().Setup(provider => provider.FolderExists(_path)).Returns(true);
            Mocker.GetMock<IDiskScanService>().Setup(service => service.GetVideoFiles(_path, true)).Returns(_decisions.Select(decision => decision.LocalEpisode.Path).ToArray());
            Mocker.GetMock<IDiskScanService>().Setup(service => service.FilterPaths(_path, It.IsAny<IEnumerable<string>>(), true)).Returns<string, IEnumerable<string>, bool>((path, files, filter) => files.ToList());
            Mocker.GetMock<IHistoryService>().Setup(service => service.FindByDownloadId("pack")).Returns(new List<EpisodeHistory>
            {
                new EpisodeHistory { EventType = EpisodeHistoryEventType.Grabbed, SeriesId = 1, EpisodeId = 301 }
            });
            Mocker.GetMock<IMakeImportDecision>().Setup(service => service.GetImportDecisions(It.IsAny<List<string>>(), _series, _download, It.IsAny<ParsedEpisodeInfo>(), It.IsAny<ParsedEpisodeInfo>(), true)).Returns(_decisions);
            Mocker.GetMock<IImportApprovedEpisodes>().Setup(service => service.Import(It.IsAny<List<ImportDecision>>(), true, _download, ImportMode.Copy)).Returns<List<ImportDecision>, bool, DownloadClientItem, ImportMode>((decisions, newDownload, item, mode) => decisions.Select(decision => new ImportResult(decision)).ToList());
        }

        [Test]
        public void should_import_only_grabbed_episodes_and_copy_files_even_if_the_client_allows_moves()
        {
            var result = Subject.ProcessPath(_path, ImportMode.Auto, _series, _download);

            result.Should().ContainSingle();
            result.Single().ImportDecision.LocalEpisode.Episodes.Single().Id.Should().Be(301);
            Mocker.GetMock<IImportApprovedEpisodes>().Verify(service => service.Import(It.Is<List<ImportDecision>>(decisions => decisions.Count == 1 && decisions[0].LocalEpisode.Episodes[0].SeasonNumber == 3), true, _download, ImportMode.Copy), Times.Once());
            Mocker.GetMock<IDiskProvider>().Verify(provider => provider.DeleteFolder(It.IsAny<string>(), It.IsAny<bool>()), Times.Never());
        }

        [Test]
        public void should_reject_when_the_requested_season_has_no_matching_file()
        {
            _decisions.RemoveAt(1);

            var result = Subject.ProcessPath(_path, ImportMode.Auto, _series, _download);

            result.Single().ImportDecision.Rejections.Single().Reason.Should().Be(ImportRejectionReason.MultiSeason);
            Mocker.GetMock<IImportApprovedEpisodes>().Verify(service => service.Import(It.IsAny<List<ImportDecision>>(), true, _download, It.IsAny<ImportMode>()), Times.Never());
        }

        [Test]
        public void should_not_assume_the_requested_season_for_an_ambiguous_combined_file()
        {
            Mocker.GetMock<IDiskScanService>().Setup(service => service.GetVideoFiles(_path, true)).Returns(new[] { _path + "/Series.Title.S01-S02.mkv", _path + "/Series.Title.S03.mkv" });

            Subject.ProcessPath(_path, ImportMode.Auto, _series, _download);

            Mocker.GetMock<IMakeImportDecision>().Verify(service => service.GetImportDecisions(It.Is<List<string>>(files => files.Count == 1 && files[0].EndsWith("Series.Title.S03.mkv")), _series, _download, It.IsAny<ParsedEpisodeInfo>(), It.IsAny<ParsedEpisodeInfo>(), true), Times.Once());
        }
    }
}

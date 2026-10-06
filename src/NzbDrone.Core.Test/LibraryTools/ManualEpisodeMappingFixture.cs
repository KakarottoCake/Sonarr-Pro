using System;
using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.History;
using NzbDrone.Core.LibraryTools;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.LibraryTools
{
    [TestFixture]
    public class ManualEpisodeMappingFixture : CoreTest<ManualEpisodeMappingService>
    {
        private LocalEpisode _local;

        [SetUp]
        public void Setup()
        {
            _local = new LocalEpisode { Series = new Series { Id = 7 }, FileEpisodeInfo = new ParsedEpisodeInfo { SeasonNumber = 3, EpisodeNumbers = new[] { 1 } } };
            var grabbed = new EpisodeHistory
            {
                SeriesId = 7, EventType = EpisodeHistoryEventType.Grabbed, Date = DateTime.UtcNow,
                SourceTitle = "Example.Show.S01-S03.COMPLETE.1080p",
                Data = new Dictionary<string, string> { { "ManualEpisodeMapping", "true" }, { "ManualEpisodeIds", "[31,32]" } }
            };
            Mocker.GetMock<IHistoryService>().Setup(h => h.FindByDownloadId("torrent")).Returns(new List<EpisodeHistory> { grabbed });
            Mocker.GetMock<IEpisodeService>().Setup(e => e.GetEpisodes(It.IsAny<IEnumerable<int>>())).Returns(new List<Episode>
            {
                new Episode { Id = 31, SeriesId = 7, SeasonNumber = 3, EpisodeNumber = 1 },
                new Episode { Id = 32, SeriesId = 7, SeasonNumber = 3, EpisodeNumber = 2 }
            });
        }

        [Test]
        public void saved_multi_season_choice_only_imports_the_selected_season()
        {
            Subject.Find(_local, "torrent", true).Episodes.Should().ContainSingle(e => e.Id == 31);
            _local.FileEpisodeInfo.SeasonNumber = 1;
            Subject.Find(_local, "torrent", true).Episodes.Should().BeEmpty();
        }

        [Test]
        public void one_file_retains_all_selected_episodes_after_restart()
        {
            Subject.Find(_local, "torrent", false).Episodes.Should().HaveCount(2);
            Mocker.GetMock<IEpisodeService>().Verify(e => e.GetEpisodes(It.IsAny<IEnumerable<int>>()), Times.Once());
        }

        [Test]
        public void ambiguous_multi_file_names_require_manual_import()
        {
            _local.FileEpisodeInfo = new ParsedEpisodeInfo { FullSeason = true, SeasonNumber = 3 };
            Subject.Find(_local, "torrent", true).Episodes.Should().BeEmpty();
        }
    }
}

using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.LibraryTools;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Qualities;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.LibraryTools
{
    [TestFixture]
    public class NamingOptionsFixture : CoreTest<FileNameBuilder>
    {
        [Test]
        public void manual_rename_works_without_changing_global_or_show_automatic_settings()
        {
            var config = new NamingConfig { RenameEpisodes = false, StandardEpisodeFormat = "{Series Title} - S{season:00}E{episode:00}" };
            Mocker.GetMock<INamingConfigService>().Setup(s => s.GetConfig()).Returns(config);
            Mocker.GetMock<IProOptionsService>().Setup(s => s.ForSeries(7)).Returns(new ProSeriesOptions { AutomaticRenaming = "manual" });
            var series = new Series { Id = 7, Title = "Example Show" };
            var episodes = new List<Episode> { new Episode { SeasonNumber = 1, EpisodeNumber = 1 } };
            var file = new EpisodeFile { RelativePath = "original.mkv", Quality = new QualityModel(Quality.HDTV1080p) };
            Mocker.GetMock<IQualityDefinitionService>().Setup(s => s.Get(Quality.HDTV1080p)).Returns(new QualityDefinition { Title = "HDTV-1080p" });
            Subject.BuildFileName(episodes, series, file).Should().Be("original");
            using (NamingContext.ForManualRename())
            {
                Subject.BuildFileName(episodes, series, file).Should().Be("Example Show - S01E01");
            }

            NamingContext.ManualRename.Should().BeFalse();
            config.RenameEpisodes.Should().BeFalse();
            Subject.BuildFileName(episodes, series, file).Should().Be("original");
        }

        [TestCase("{SeasonTitle} ({SeasonYear})", "Arc One (2021)")]
        [TestCase("{Season Title} ({Season Year})", "Arc One (2021)")]
        public void uses_season_metadata_and_first_episode_year(string format, string expected)
        {
            Mocker.GetMock<IProOptionsService>().Setup(s => s.ForSeries(7)).Returns(new ProSeriesOptions { SeasonTitles = new Dictionary<int, string> { { 2, "" } } });
            Mocker.GetMock<IEpisodeService>().Setup(s => s.GetEpisodesBySeason(7, 2)).Returns(new List<Episode> { new Episode { AirDateUtc = new DateTime(2021, 4, 1) } });
            var series = new Series { Id = 7, Seasons = new List<Season> { new Season { SeasonNumber = 2, Title = "Arc One" } } };
            Subject.GetSeasonFolder(series, 2, new NamingConfig { SeasonFolderFormat = format }).Should().Be(expected);
        }
    }
}

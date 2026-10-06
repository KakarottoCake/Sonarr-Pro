using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class AnimePackMatcherFixture
    {
        private AnimeSeasonSearchCriteria _criteria;

        [SetUp]
        public void Setup()
        {
            _criteria = new AnimeSeasonSearchCriteria
            {
                Series = new Series { Title = "Example Anime", Seasons = new List<Season> { new Season { SeasonNumber = 1 } } },
                SeasonNumber = 1,
                SceneTitles = new List<string> { "Example Anime" },
                CatalogueEpisodes = Enumerable.Range(1, 12).Select(n => new Episode { SeasonNumber = 1, EpisodeNumber = n, AirDateUtc = DateTime.UtcNow.AddDays(-1) }).ToList()
            };
        }

        [TestCase("[EMBER] Example Anime 01-12 Complete 1080p")]
        [TestCase("Example.Anime.1-12.Batch.1080p")]
        public void identifies_an_explicit_complete_season(string title)
        {
            var result = AnimePackMatcher.Match(title, _criteria);
            result.Should().NotBeNull();
            result.FullSeason.Should().BeTrue();
            result.SeasonNumber.Should().Be(1);
        }

        [TestCase("Example Anime 01-11 Complete 1080p")]
        [TestCase("Example Anime Complete 1080p")]
        [TestCase("Example Anime Next 01-12 Complete 1080p")]
        [TestCase("Example Anime 01-12 Complete OVA 1080p")]
        [TestCase("Example Anime 01-12 1080p")]
        public void rejects_partial_or_ambiguous_packs(string title)
        {
            AnimePackMatcher.Match(title, _criteria).Should().BeNull();
        }

        [Test]
        public void does_not_infer_a_season_from_a_multi_season_catalogue()
        {
            _criteria.Series.Seasons.Add(new Season { SeasonNumber = 2 });
            AnimePackMatcher.Match("Example Anime 01-12 Complete 1080p", _criteria).Should().BeNull();
            _criteria.SeasonSceneTitles.Add("Example Anime Arc One");
            AnimePackMatcher.Match("Example Anime Arc One 01-12 Complete 1080p", _criteria).Should().NotBeNull();
        }

        [Test]
        public void rejects_an_unfinished_season()
        {
            _criteria.CatalogueEpisodes[11].AirDateUtc = DateTime.UtcNow.AddDays(1);
            AnimePackMatcher.Match("Example Anime 01-12 Complete 1080p", _criteria).Should().BeNull();
        }
    }
}

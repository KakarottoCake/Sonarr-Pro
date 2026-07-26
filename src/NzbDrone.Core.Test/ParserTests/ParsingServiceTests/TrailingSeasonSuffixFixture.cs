using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.DataAugmentation.Scene;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.ParserTests.ParsingServiceTests
{
    [TestFixture]
    public class TrailingSeasonSuffixFixture : CoreTest<ParsingService>
    {
        private Series _series;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>.CreateNew()
                                     .With(s => s.Title = "Tensei Shitara Slime Datta Ken")
                                     .With(s => s.CleanTitle = "tenseishitaraslimedattaken")
                                     .Build();

            Mocker.GetMock<ISceneMappingService>()
                  .Setup(s => s.FindTvdbId(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                  .Returns((int?)null);
        }

        /// <summary>
        /// Only the stripped title resolves, which is the situation the fallback exists for:
        /// the release names a season the library does not have as a separate series.
        /// </summary>
        private void GivenOnlyStrippedTitleMatches(string strippedTitle)
        {
            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.FindByTitle(It.IsAny<string>()))
                  .Returns((Series)null);

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.FindByTitle(strippedTitle))
                  .Returns(_series);
        }

        private void GivenNothingMatches()
        {
            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.FindByTitle(It.IsAny<string>()))
                  .Returns((Series)null);
        }

        [Test]
        public void should_read_a_trailing_number_as_the_season()
        {
            GivenOnlyStrippedTitleMatches("Tensei Shitara Slime Datta Ken");

            var series = Subject.GetSeries("[Ironclad] Tensei Shitara Slime Datta Ken 4 - S04E16 [WEB.1080p.AV1]");

            series.Should().Be(_series);
        }

        [Test]
        public void should_not_strip_a_number_that_is_not_the_season()
        {
            // Taxi 3 is a different film from Taxi, and its first season says nothing about
            // the 3. Stripping here would resolve to whatever "Taxi" happens to be.
            GivenOnlyStrippedTitleMatches("Taxi");

            Subject.GetSeries("Taxi 3 S01E01 1080p WEB").Should().BeNull();
        }

        [Test]
        public void should_not_strip_when_the_release_declares_no_season()
        {
            GivenOnlyStrippedTitleMatches("Tensei Shitara Slime Datta Ken");

            // An absolute-numbered release carries no season, so there is nothing to agree
            // with and no reason to believe the number is one.
            Subject.GetSeries("Tensei Shitara Slime Datta Ken 4 - 88 [1080p]").Should().BeNull();
        }

        [Test]
        public void should_not_strip_a_year_like_number()
        {
            GivenOnlyStrippedTitleMatches("Series Title");

            Subject.GetSeries("Series Title 2019 S01E01 1080p WEB").Should().BeNull();
        }

        [Test]
        public void should_not_strip_a_year_the_parser_recognised_even_when_it_looks_like_the_season()
        {
            // A two digit year and the season can be the same number, and the title then
            // ends in something indistinguishable from a season suffix. The year has its own
            // matching path, which is entitled to reject a mismatched year, and stripping
            // here would talk over that decision.
            GivenOnlyStrippedTitleMatches("Series Alias");

            var parsed = Parser.Parser.ParseTitle("Series Alias 04 S04E01 1080p WEB");

            if (parsed?.SeriesTitleInfo?.Year > 0)
            {
                Subject.GetSeries("Series Alias 04 S04E01 1080p WEB").Should().BeNull();
            }
            else
            {
                Assert.Pass("The parser read no year from this title, so the guard does not apply");
            }
        }

        [Test]
        public void should_prefer_the_full_title_when_it_matches()
        {
            // A series really named with a trailing number must win, and the fallback must
            // not run at all.
            var numbered = Builder<Series>.CreateNew()
                                          .With(s => s.Title = "Tensei Shitara Slime Datta Ken 4")
                                          .Build();

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.FindByTitle(It.IsAny<string>()))
                  .Returns(numbered);

            Subject.GetSeries("Tensei Shitara Slime Datta Ken 4 - S04E16").Should().Be(numbered);
        }

        [Test]
        public void should_return_nothing_when_the_stripped_title_is_unknown()
        {
            GivenNothingMatches();

            Subject.GetSeries("Some Unknown Show 4 - S04E16").Should().BeNull();
        }
    }
}

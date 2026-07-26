using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.TvTests.SeriesServiceTests
{
    [TestFixture]
    public class FindByAlternateTitleFixture : CoreTest<SeriesService>
    {
        private Series _series;

        [SetUp]
        public void Setup()
        {
            // The real case: AniList names One Piece's Fish-Man Island recut by its romaji
            // title, while releases of it are named with the English one the same provider
            // publishes.
            _series = new Series
            {
                Id = 1,
                Title = "ONE PIECE: Gyojin Tou-hen",
                CleanTitle = "onepiecegyojintouhen",
                AlternateTitles = new List<string> { "One Piece Log: Fish-Man Island Saga" }
            };
        }

        private void GivenCanonicalMatch(Series series)
        {
            Mocker.GetMock<ISeriesRepository>()
                  .Setup(s => s.FindByTitle(It.IsAny<string>()))
                  .Returns(series);
        }

        private void GivenAlternateMatch(Series series)
        {
            Mocker.GetMock<ISeriesRepository>()
                  .Setup(s => s.FindByAlternateTitle(It.IsAny<string>()))
                  .Returns(series);
        }

        [Test]
        public void should_use_the_canonical_title_when_it_matches()
        {
            GivenCanonicalMatch(_series);

            Subject.FindByTitle("ONE PIECE Gyojin Tou-hen").Should().Be(_series);

            Mocker.GetMock<ISeriesRepository>()
                  .Verify(v => v.FindByAlternateTitle(It.IsAny<string>()), Times.Never());
        }

        [Test]
        public void should_fall_back_to_an_alternate_title()
        {
            GivenCanonicalMatch(null);
            GivenAlternateMatch(_series);

            Subject.FindByTitle("One Piece Log Fish-Man Island Saga").Should().Be(_series);
        }

        [Test]
        public void should_return_nothing_when_neither_matches()
        {
            // The fallback adds more exact names to compare against. It must not turn a
            // miss into a guess, since this decides which series an unattended download
            // belongs to.
            GivenCanonicalMatch(null);
            GivenAlternateMatch(null);

            Subject.FindByTitle("Something Else Entirely").Should().BeNull();
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.MetadataSource
{
    /// <summary>
    /// Reproduces what AniList actually returns for an abbreviated query, so the ranking is
    /// exercised against real shapes rather than convenient ones.
    /// </summary>
    [TestFixture]
    public class SynonymRankingFixture : CoreTest
    {
        private static Series Result(string title, int votes, params string[] alternateTitles)
        {
            return new Series
            {
                Title = title,
                AlternateTitles = alternateTitles.ToList(),
                Ratings = new Ratings { Votes = votes }
            };
        }

        /// <summary>
        /// The order and shape AniList really returns for "AoT". Every entry in the franchise
        /// carries the abbreviation, including the specials and spin-offs, so the name alone
        /// cannot pick out the series someone meant.
        /// </summary>
        private static List<Series> RealAniListResultsForAot()
        {
            return new List<Series>
            {
                Result("Shingeki no Kyotou", 1200, "AoT"),
                Result("Shingeki no Kyojin Season 2: Kakusei no Houkou", 8000, "AoT"),
                Result("Shingeki no Kyojin", 700000, "Attack on Titan", "SnK", "AoT"),
                Result("Shingeki no Kyojin: LOST GIRLS", 20000, "AoT"),
                Result("Shingeki no Kyojin Gaiden: Kuinaki Sentaku", 30000, "AoT")
            };
        }

        [Test]
        public void should_rank_the_main_series_first_for_an_abbreviation()
        {
            var series = RealAniListResultsForAot();

            series.Sort(new SearchSeriesComparer("AoT"));

            series.First().Title.Should().Be("Shingeki no Kyojin");
        }

        [Test]
        public void should_fall_back_to_popularity_only_when_names_match_equally()
        {
            // A closer name still wins over a more popular one, so the tiebreak cannot drag
            // a well-known series above an exact match for something else.
            var series = new List<Series>
            {
                Result("Shingeki no Kyojin", 700000, "AoT"),
                Result("Shingeki no Kyotou", 1200)
            };

            series.Sort(new SearchSeriesComparer("Shingeki no Kyotou"));

            series.First().Title.Should().Be("Shingeki no Kyotou");
        }

        [Test]
        public void should_handle_a_series_with_no_ratings()
        {
            var series = new List<Series>
            {
                new Series { Title = "Shingeki no Kyotou", AlternateTitles = new List<string> { "AoT" } },
                Result("Shingeki no Kyojin", 700000, "AoT")
            };

            series.Sort(new SearchSeriesComparer("AoT"));

            series.First().Title.Should().Be("Shingeki no Kyojin");
        }

        [Test]
        public void should_score_an_abbreviation_synonym_as_an_exact_match()
        {
            SeriesTitleMatcher.Score("AoT", "AoT").Should().Be(1);
        }

        [Test]
        public void should_score_an_unrelated_title_below_an_exact_synonym()
        {
            var unrelated = SeriesTitleMatcher.Score("AoT", "Shingeki no Kyotou");

            unrelated.Should().BeLessThan(1);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.MetadataSource
{
    [TestFixture]
    public class SearchSeriesComparerFixture : CoreTest
    {
        private List<Series> _series;

        [SetUp]
        public void Setup()
        {
            _series = new List<Series>();
        }

        private void WithSeries(string title)
        {
            _series.Add(new Series { Title = title });
        }

        private void WithSeries(string title, params string[] alternateTitles)
        {
            _series.Add(new Series { Title = title, AlternateTitles = alternateTitles.ToList() });
        }

        [Test]
        public void should_rank_on_an_alternate_title_when_that_is_what_was_typed()
        {
            // AniList returns the series because it matched a synonym server-side. Ranking
            // on the canonical title alone would then put it last, which is the opposite of
            // what the person searching expects.
            WithSeries("Attack on Titan Junior High");
            WithSeries("Shingeki no Kyojin", "Attack on Titan", "AoT", "SnK");

            _series.Sort(new SearchSeriesComparer("AoT"));

            _series.First().Title.Should().Be("Shingeki no Kyojin");
        }

        [Test]
        public void should_rank_on_an_english_alternate_title()
        {
            WithSeries("Attack of the Killer Tomatoes");
            WithSeries("Shingeki no Kyojin", "Attack on Titan");

            _series.Sort(new SearchSeriesComparer("attack on titan"));

            _series.First().Title.Should().Be("Shingeki no Kyojin");
        }

        [Test]
        public void should_still_prefer_a_canonical_title_match_over_a_weaker_synonym()
        {
            WithSeries("Steins;Gate 0", "Zero");
            WithSeries("Steins;Gate", "STEINS;GATE");

            _series.Sort(new SearchSeriesComparer("steins gate"));

            _series.First().Title.Should().Be("Steins;Gate");
        }

        [Test]
        public void should_handle_a_series_with_no_alternate_titles()
        {
            // Series from providers that publish none, and anything constructed elsewhere,
            // must not trip the comparer.
            _series.Add(new Series { Title = "Breaking Bad", AlternateTitles = null });
            WithSeries("Breaking In");

            _series.Sort(new SearchSeriesComparer("breaking bad"));

            _series.First().Title.Should().Be("Breaking Bad");
        }

        [Test]
        public void should_prefer_the_walking_dead_over_talking_dead_when_searching_for_the_walking_dead()
        {
            WithSeries("Talking Dead");
            WithSeries("The Walking Dead");

            _series.Sort(new SearchSeriesComparer("the walking dead"));

            _series.First().Title.Should().Be("The Walking Dead");
        }

        [Test]
        public void should_prefer_the_walking_dead_over_talking_dead_when_searching_for_walking_dead()
        {
            WithSeries("Talking Dead");
            WithSeries("The Walking Dead");

            _series.Sort(new SearchSeriesComparer("walking dead"));

            _series.First().Title.Should().Be("The Walking Dead");
        }

        [Test]
        public void should_prefer_blacklist_over_the_blacklist_when_searching_for_blacklist()
        {
            WithSeries("The Blacklist");
            WithSeries("Blacklist");

            _series.Sort(new SearchSeriesComparer("blacklist"));

            _series.First().Title.Should().Be("Blacklist");
        }

        [Test]
        public void should_prefer_the_blacklist_over_blacklist_when_searching_for_the_blacklist()
        {
            WithSeries("Blacklist");
            WithSeries("The Blacklist");

            _series.Sort(new SearchSeriesComparer("the blacklist"));

            _series.First().Title.Should().Be("The Blacklist");
        }
    }
}

using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MetadataSource
{
    [TestFixture]
    public class SeriesTitleMatcherFixture : CoreTest
    {
        [TestCase("Attack on Titan Season 2", "attack on titan season 2")]
        [TestCase("Attack on Titan 2nd Season", "attack on titan season 2")]
        [TestCase("Attack on Titan S2", "attack on titan season 2")]
        [TestCase("Attack on Titan Part 2", "attack on titan season 2")]
        [TestCase("Attack on Titan II", "attack on titan season 2")]
        [TestCase("Attack on Titan 3rd Season", "attack on titan season 3")]
        public void should_write_every_season_form_the_same_way(string title, string expected)
        {
            // Anime seasons are written half a dozen ways; without folding them together the
            // same season reads as an unrelated title.
            SeriesTitleMatcher.Normalize(title).Should().Be(expected);
        }

        [TestCase("The Blacklist", "blacklist")]
        [TestCase("A Certain Magical Index", "certain magical index")]
        [TestCase("Steins;Gate", "steins gate")]
        [TestCase("Re:ZERO -Starting Life in Another World-", "re zero starting life in another world")]
        [TestCase("Fate/Zero", "fate zero")]
        [TestCase("Pokémon", "pokemon")]
        public void should_reduce_titles_to_a_comparable_form(string title, string expected)
        {
            SeriesTitleMatcher.Normalize(title).Should().Be(expected);
        }

        [Test]
        public void should_score_an_exact_match_highest()
        {
            SeriesTitleMatcher.Score("Cowboy Bebop", "Cowboy Bebop").Should().Be(1);
        }

        [Test]
        public void should_score_a_match_that_differs_only_in_punctuation_as_exact()
        {
            SeriesTitleMatcher.Score("steins gate", "Steins;Gate").Should().Be(1);
        }

        [Test]
        public void should_treat_differently_written_seasons_as_the_same()
        {
            SeriesTitleMatcher.Score("Attack on Titan 2nd Season", "Attack on Titan Season 2").Should().Be(1);
        }

        [Test]
        public void should_rank_a_prefix_of_the_title_highly()
        {
            SeriesTitleMatcher.Score("cowboy", "Cowboy Bebop").Should().BeGreaterThan(0.9);
        }

        [Test]
        public void should_tolerate_a_typo()
        {
            // The old comparer allowed a single character of difference and nothing more.
            SeriesTitleMatcher.Score("cowbay bebop", "Cowboy Bebop").Should().BeGreaterThan(0.85);
        }

        [Test]
        public void should_prefer_the_closer_of_two_candidates()
        {
            var exact = SeriesTitleMatcher.Score("the blacklist", "The Blacklist");
            var other = SeriesTitleMatcher.Score("the blacklist", "Blacklist Redemption");

            exact.Should().BeGreaterThan(other);
        }

        [Test]
        public void should_score_unrelated_titles_low()
        {
            SeriesTitleMatcher.Score("Cowboy Bebop", "Breaking Bad").Should().BeLessThan(0.6);
        }

        [Test]
        public void should_not_confuse_different_seasons_of_the_same_series()
        {
            var same = SeriesTitleMatcher.Score("Attack on Titan Season 2", "Attack on Titan 2nd Season");
            var different = SeriesTitleMatcher.Score("Attack on Titan Season 2", "Attack on Titan Season 3");

            same.Should().BeGreaterThan(different);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void should_handle_missing_input(string value)
        {
            SeriesTitleMatcher.Normalize(value).Should().BeEmpty();
            SeriesTitleMatcher.Score(value, "Cowboy Bebop").Should().Be(0);
            SeriesTitleMatcher.Score("Cowboy Bebop", value).Should().Be(0);
        }
    }
}

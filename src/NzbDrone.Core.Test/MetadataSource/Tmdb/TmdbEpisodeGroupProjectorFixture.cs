using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MetadataSource.Tmdb;
using NzbDrone.Core.MetadataSource.Tmdb.Resource;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.MetadataSource.Tmdb
{
    [TestFixture]
    public class TmdbEpisodeGroupProjectorFixture : CoreTest
    {
        private static Episode Episode(int tmdbId, int season, int number)
        {
            return new Episode
            {
                ForeignId = tmdbId.ToString(),
                SeasonNumber = season,
                EpisodeNumber = number,
                Title = $"S{season}E{number}"
            };
        }

        private static TmdbEpisodeGroupDetailResource Group(params (int Order, int[] EpisodeIds)[] groups)
        {
            return new TmdbEpisodeGroupDetailResource
            {
                Id = "group-1",
                Type = (int)TmdbEpisodeGroupType.Absolute,
                Groups = groups.Select(g => new TmdbEpisodeGroupItemResource
                {
                    Order = g.Order,
                    Episodes = g.EpisodeIds.Select((id, index) => new TmdbEpisodeGroupEpisodeResource
                    {
                        Id = id,
                        Order = index
                    }).ToList()
                }).ToList()
            };
        }

        [Test]
        public void should_renumber_seasons_into_one_absolute_run()
        {
            // Two aired seasons of two episodes each, published as a single absolute group.
            var episodes = new List<Episode>
            {
                Episode(101, 1, 1),
                Episode(102, 1, 2),
                Episode(201, 2, 1),
                Episode(202, 2, 2)
            };

            var result = TmdbEpisodeGroupProjector.Project(Group((1, new[] { 101, 102, 201, 202 })), episodes);

            result.Should().HaveCount(4);
            result.Select(e => e.EpisodeNumber).Should().Equal(1, 2, 3, 4);
            result.Should().OnlyContain(e => e.SeasonNumber == 1);
        }

        [Test]
        public void should_use_group_order_as_season_number()
        {
            var episodes = new List<Episode> { Episode(101, 5, 9), Episode(102, 5, 10) };

            var result = TmdbEpisodeGroupProjector.Project(Group((3, new[] { 101 }), (4, new[] { 102 })), episodes);

            result.Single(e => e.ForeignId == "101").SeasonNumber.Should().Be(3);
            result.Single(e => e.ForeignId == "102").SeasonNumber.Should().Be(4);
        }

        [Test]
        public void should_restart_episode_numbering_in_each_group()
        {
            var episodes = new List<Episode> { Episode(101, 1, 1), Episode(102, 1, 2), Episode(103, 1, 3) };

            var result = TmdbEpisodeGroupProjector.Project(Group((1, new[] { 101, 102 }), (2, new[] { 103 })), episodes);

            result.Single(e => e.ForeignId == "101").EpisodeNumber.Should().Be(1);
            result.Single(e => e.ForeignId == "102").EpisodeNumber.Should().Be(2);
            result.Single(e => e.ForeignId == "103").EpisodeNumber.Should().Be(1);
        }

        [Test]
        public void should_order_groups_by_order_not_by_listed_position()
        {
            var episodes = new List<Episode> { Episode(101, 1, 1), Episode(102, 1, 2) };

            // Groups arrive out of order; the later group must not claim episode number 1.
            var result = TmdbEpisodeGroupProjector.Project(Group((2, new[] { 102 }), (1, new[] { 101 })), episodes);

            result.First().ForeignId.Should().Be("101");
            result.Single(e => e.ForeignId == "101").SeasonNumber.Should().Be(1);
            result.Single(e => e.ForeignId == "102").SeasonNumber.Should().Be(2);
        }

        [Test]
        public void should_keep_episodes_the_ordering_does_not_mention()
        {
            // An episode missing from the group must survive, otherwise any file already
            // matched to it would be orphaned.
            var episodes = new List<Episode> { Episode(101, 1, 1), Episode(999, 0, 1) };

            var result = TmdbEpisodeGroupProjector.Project(Group((1, new[] { 101 })), episodes);

            result.Should().HaveCount(2);
            result.Should().Contain(e => e.ForeignId == "999");
        }

        [Test]
        public void should_not_let_a_duplicated_entry_consume_two_episode_numbers()
        {
            var episodes = new List<Episode> { Episode(101, 1, 1), Episode(102, 1, 2) };

            var result = TmdbEpisodeGroupProjector.Project(Group((1, new[] { 101, 101, 102 })), episodes);

            result.Should().HaveCount(2);
            result.Single(e => e.ForeignId == "102").EpisodeNumber.Should().Be(2);
        }

        [Test]
        public void should_return_all_episodes_when_group_is_empty()
        {
            var episodes = new List<Episode> { Episode(101, 1, 1), Episode(102, 1, 2) };

            var result = TmdbEpisodeGroupProjector.Project(new TmdbEpisodeGroupDetailResource(), episodes);

            result.Should().HaveCount(2);
            result.Single(e => e.ForeignId == "101").EpisodeNumber.Should().Be(1);
        }

        [Test]
        public void should_return_all_episodes_when_group_is_null()
        {
            var episodes = new List<Episode> { Episode(101, 1, 1) };

            var result = TmdbEpisodeGroupProjector.Project(null, episodes);

            result.Should().HaveCount(1);
        }

        [Test]
        public void should_ignore_episodes_without_a_parsable_foreign_id()
        {
            var episodes = new List<Episode> { Episode(101, 1, 1), new Episode { ForeignId = null, SeasonNumber = 1, EpisodeNumber = 7 } };

            var result = TmdbEpisodeGroupProjector.Project(Group((1, new[] { 101 })), episodes);

            result.Should().HaveCount(1);
            result.Single().ForeignId.Should().Be("101");
        }

        [Test]
        public void should_map_absolute_group_type_to_absolute_ordering()
        {
            var ordering = TmdbEpisodeGroupProjector.ToOrdering(new TmdbEpisodeGroupSummaryResource
            {
                Id = "abc",
                Name = "Absolute",
                EpisodeCount = 1100,
                GroupCount = 1,
                Type = (int)TmdbEpisodeGroupType.Absolute
            });

            ordering.IsAbsolute.Should().BeTrue();
            ordering.IsDefault.Should().BeFalse();
            ordering.EpisodeCount.Should().Be(1100);
        }

        [Test]
        public void should_not_flag_non_absolute_group_as_absolute()
        {
            var ordering = TmdbEpisodeGroupProjector.ToOrdering(new TmdbEpisodeGroupSummaryResource
            {
                Id = "abc",
                Name = "DVD Order",
                Type = (int)TmdbEpisodeGroupType.Dvd
            });

            ordering.IsAbsolute.Should().BeFalse();
        }
    }
}

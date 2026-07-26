using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MetadataSource.AniList;
using NzbDrone.Core.MetadataSource.AniList.Resource;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MetadataSource.AniList
{
    [TestFixture]
    public class AniListEpisodeMapperFixture : CoreTest
    {
        private static AniListMediaResource Media(int? episodes, params (int Episode, long AiringAt)[] schedule)
        {
            return new AniListMediaResource
            {
                Id = 21,
                Episodes = episodes,
                AiringSchedule = new AniListAiringScheduleConnection
                {
                    Nodes = schedule.Select(s => new AniListAiringScheduleResource
                    {
                        Episode = s.Episode,
                        AiringAt = s.AiringAt
                    }).ToList()
                }
            };
        }

        [Test]
        public void should_number_episodes_from_one_in_a_single_season()
        {
            var result = AniListEpisodeMapper.MapEpisodes(Media(3));

            result.Should().HaveCount(3);
            result.Select(e => e.EpisodeNumber).Should().Equal(1, 2, 3);
            result.Should().OnlyContain(e => e.SeasonNumber == 1);
        }

        [Test]
        public void should_set_absolute_numbers_to_match_episode_numbers()
        {
            // Anime releases are named by absolute number, so these must agree.
            var result = AniListEpisodeMapper.MapEpisodes(Media(5));

            result.Should().OnlyContain(e => e.AbsoluteEpisodeNumber == e.EpisodeNumber);
        }

        [Test]
        public void should_use_airing_schedule_when_it_exceeds_the_declared_count()
        {
            // While a series airs, AniList leaves Episodes null but keeps publishing the
            // schedule. Truncating to the declared count would hide aired episodes.
            var result = AniListEpisodeMapper.MapEpisodes(Media(null, (1, 1600000000), (2, 1600604800), (3, 1601209600)));

            result.Should().HaveCount(3);
        }

        [Test]
        public void should_use_declared_count_when_it_exceeds_the_schedule()
        {
            var result = AniListEpisodeMapper.MapEpisodes(Media(12, (1, 1600000000)));

            result.Should().HaveCount(12);
        }

        [Test]
        public void should_convert_airing_timestamps_to_utc_air_dates()
        {
            var result = AniListEpisodeMapper.MapEpisodes(Media(1, (1, 1600000000)));

            var expected = DateTimeOffset.FromUnixTimeSeconds(1600000000).UtcDateTime;

            result.Single().AirDateUtc.Should().Be(expected);
            result.Single().AirDate.Should().Be(expected.ToString("yyyy-MM-dd"));
        }

        [Test]
        public void should_leave_air_date_null_when_not_scheduled()
        {
            var result = AniListEpisodeMapper.MapEpisodes(Media(2, (1, 1600000000)));

            result.Single(e => e.EpisodeNumber == 2).AirDateUtc.Should().BeNull();
        }

        [Test]
        public void should_give_each_episode_a_stable_foreign_id()
        {
            var result = AniListEpisodeMapper.MapEpisodes(Media(2));

            result.Select(e => e.ForeignId).Should().Equal("21:1", "21:2");
        }

        [Test]
        public void should_return_no_episodes_when_count_is_unknown_and_nothing_is_scheduled()
        {
            var result = AniListEpisodeMapper.MapEpisodes(Media(null));

            result.Should().BeEmpty();
        }

        [Test]
        public void should_handle_a_missing_airing_schedule()
        {
            var result = AniListEpisodeMapper.MapEpisodes(new AniListMediaResource { Id = 21, Episodes = 2 });

            result.Should().HaveCount(2);
            result.Should().OnlyContain(e => e.AirDateUtc == null);
        }

        [Test]
        public void should_prefer_the_english_title()
        {
            // AniList's own preference is romaji, which is what the library would otherwise
            // be named in, including on disk.
            var media = new AniListMediaResource
            {
                Id = 16498,
                Title = new AniListTitleResource
                {
                    Romaji = "Shingeki no Kyojin",
                    English = "Attack on Titan",
                    Native = "進撃の巨人"
                },
                Synonyms = new List<string> { "AoT", "SnK" }
            };

            var titles = AniListEpisodeMapper.GetAllTitles(media);

            titles.First().Should().Be("Attack on Titan");
        }

        [Test]
        public void should_keep_the_romaji_title_for_matching()
        {
            // Most releases are named in romaji, so it has to survive as an alternate or
            // preferring English would stop them matching.
            var media = new AniListMediaResource
            {
                Id = 16498,
                Title = new AniListTitleResource
                {
                    Romaji = "Shingeki no Kyojin",
                    English = "Attack on Titan",
                    Native = "進撃の巨人"
                },
                Synonyms = new List<string> { "AoT", "SnK" }
            };

            var titles = AniListEpisodeMapper.GetAllTitles(media);

            titles.Should().Contain("Shingeki no Kyojin");
            titles.Should().Contain("AoT");
            titles.Should().Contain("SnK");
        }

        [Test]
        public void should_fall_back_to_romaji_when_there_is_no_english_title()
        {
            // Plenty of anime never gets an English title, and the series still needs a name.
            var media = new AniListMediaResource
            {
                Id = 1,
                Title = new AniListTitleResource
                {
                    Romaji = "Yofukashi no Uta",
                    English = null,
                    Native = "よふかしのうた"
                }
            };

            AniListEpisodeMapper.GetAllTitles(media).First().Should().Be("Yofukashi no Uta");
        }

        [Test]
        public void should_not_repeat_titles_that_are_the_same()
        {
            var media = new AniListMediaResource
            {
                Id = 1,
                Title = new AniListTitleResource
                {
                    Romaji = "Steins;Gate",
                    English = "Steins;Gate",
                    UserPreferred = "steins;gate"
                },
                Synonyms = new List<string> { "Steins;Gate" }
            };

            AniListEpisodeMapper.GetAllTitles(media).Should().ContainSingle();
        }

        [Test]
        public void should_ignore_blank_titles()
        {
            var media = new AniListMediaResource
            {
                Id = 1,
                Title = new AniListTitleResource { Romaji = "One Piece", English = "", Native = null },
                Synonyms = new List<string> { "  " }
            };

            AniListEpisodeMapper.GetAllTitles(media).Should().Equal("One Piece");
        }
    }
}

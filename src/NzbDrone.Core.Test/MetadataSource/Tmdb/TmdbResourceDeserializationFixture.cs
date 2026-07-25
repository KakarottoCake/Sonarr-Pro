using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.MetadataSource.Jikan.Resource;
using NzbDrone.Core.MetadataSource.Tmdb.Resource;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MetadataSource.Tmdb
{
    /// <summary>
    /// Deserializes provider payloads the way the HTTP client does.
    /// <para>
    /// HttpResponse&lt;T&gt; uses the Newtonsoft serializer, which is configured with a
    /// camel-case contract resolver and ignores System.Text.Json's JsonPropertyName. Any
    /// snake_case field annotated with the wrong attribute silently deserializes to its
    /// default instead of failing, so the mapping has to be asserted against real payload
    /// shapes rather than hand-built objects.
    /// </para>
    /// </summary>
    [TestFixture]
    public class TmdbResourceDeserializationFixture : CoreTest
    {
        [Test]
        public void should_map_snake_case_fields_on_episode_group_list()
        {
            var json = @"{
                ""results"": [
                    { ""id"": ""5ad0f096"", ""name"": ""Absolute (No Specials)"", ""type"": 2,
                      ""episode_count"": 1181, ""group_count"": 1 }
                ]
            }";

            var resource = Json.Deserialize<TmdbEpisodeGroupListResource>(json);

            var group = resource.Results.Should().ContainSingle().Subject;

            group.EpisodeCount.Should().Be(1181);
            group.GroupCount.Should().Be(1);
            group.Type.Should().Be(2);
        }

        [Test]
        public void should_map_snake_case_fields_on_episode_group_detail()
        {
            var json = @"{
                ""id"": ""5ad0f096"", ""name"": ""Absolute"", ""type"": 2,
                ""groups"": [
                    { ""id"": ""g1"", ""name"": ""One Piece"", ""order"": 1,
                      ""episodes"": [
                        { ""id"": 62085, ""order"": 0, ""episode_number"": 1, ""season_number"": 1,
                          ""air_date"": ""1999-10-20"", ""still_path"": ""/a.jpg"" }
                      ] }
                ]
            }";

            var resource = Json.Deserialize<TmdbEpisodeGroupDetailResource>(json);

            var episode = resource.Groups.Should().ContainSingle().Subject
                                  .Episodes.Should().ContainSingle().Subject;

            episode.EpisodeNumber.Should().Be(1);
            episode.SeasonNumber.Should().Be(1);
            episode.AirDate.Should().Be("1999-10-20");
            episode.StillPath.Should().Be("/a.jpg");
        }

        [Test]
        public void should_map_snake_case_fields_on_series()
        {
            var json = @"{
                ""id"": 37854, ""name"": ""One Piece"",
                ""first_air_date"": ""1999-10-20"", ""last_air_date"": ""2024-01-01"",
                ""episode_run_time"": [24], ""original_language"": ""ja"",
                ""origin_country"": [""JP""], ""vote_average"": 8.7, ""vote_count"": 4000,
                ""poster_path"": ""/p.jpg"", ""backdrop_path"": ""/b.jpg"",
                ""external_ids"": { ""imdb_id"": ""tt0388629"", ""tvdb_id"": 81797 },
                ""seasons"": [ { ""id"": 1, ""season_number"": 1, ""episode_count"": 61 } ]
            }";

            var resource = Json.Deserialize<TmdbSeriesResource>(json);

            resource.FirstAirDate.Should().Be("1999-10-20");
            resource.EpisodeRunTime.Should().Equal(24);
            resource.OriginalLanguage.Should().Be("ja");
            resource.OriginCountry.Should().Equal("JP");
            resource.VoteAverage.Should().Be(8.7m);
            resource.PosterPath.Should().Be("/p.jpg");
            resource.ExternalIds.ImdbId.Should().Be("tt0388629");
            resource.ExternalIds.TvdbId.Should().Be(81797);
            resource.Seasons.Single().SeasonNumber.Should().Be(1);
            resource.Seasons.Single().EpisodeCount.Should().Be(61);
        }

        [Test]
        public void should_map_snake_case_fields_on_season_detail()
        {
            var json = @"{
                ""season_number"": 1,
                ""episodes"": [
                    { ""id"": 62085, ""episode_number"": 1, ""season_number"": 1,
                      ""air_date"": ""1999-10-20"", ""still_path"": ""/s.jpg"",
                      ""vote_average"": 7.5, ""vote_count"": 10 }
                ]
            }";

            var resource = Json.Deserialize<TmdbSeasonDetailResource>(json);

            var episode = resource.Episodes.Should().ContainSingle().Subject;

            episode.EpisodeNumber.Should().Be(1);
            episode.AirDate.Should().Be("1999-10-20");
            episode.StillPath.Should().Be("/s.jpg");
            episode.VoteCount.Should().Be(10);
        }

        [Test]
        public void should_map_snake_case_fields_on_jikan_anime()
        {
            var json = @"{
                ""data"": {
                    ""mal_id"": 21, ""title"": ""One Piece"", ""title_english"": ""One Piece"",
                    ""title_japanese"": ""ワンピース"", ""episodes"": 1100,
                    ""duration"": ""24 min per ep"", ""score"": 8.7, ""scored_by"": 1000,
                    ""images"": { ""jpg"": { ""large_image_url"": ""/l.jpg"", ""image_url"": ""/i.jpg"" } }
                }
            }";

            var anime = Json.Deserialize<JikanAnimeResponse>(json).Data;

            anime.MalId.Should().Be(21);
            anime.TitleEnglish.Should().Be("One Piece");
            anime.ScoredBy.Should().Be(1000);
            anime.Images.Jpg.LargeImageUrl.Should().Be("/l.jpg");
        }

        [Test]
        public void should_map_snake_case_fields_on_jikan_episode_list()
        {
            var json = @"{
                ""pagination"": { ""last_visible_page"": 5, ""has_next_page"": true },
                ""data"": [ { ""mal_id"": 1, ""title"": ""Romance Dawn"", ""filler"": false, ""recap"": false } ]
            }";

            var resource = Json.Deserialize<JikanEpisodeListResponse>(json);

            resource.Pagination.LastVisiblePage.Should().Be(5);
            resource.Pagination.HasNextPage.Should().BeTrue();
            resource.Data.Single().MalId.Should().Be(1);
        }
    }
}

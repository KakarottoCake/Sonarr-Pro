using System.Linq;
using System.Text;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.ImportLists.AniList.List;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.ImportListTests
{
    [TestFixture]
    public class AniListParserFixture : CoreTest
    {
        private static ImportListResponse Response(string content)
        {
            var request = new HttpRequest("https://graphql.anilist.co");
            var response = new HttpResponse(request, new HttpHeader(), Encoding.UTF8.GetBytes(content));

            return new ImportListResponse(new ImportListRequest(request), response);
        }

        /// <summary>
        /// Two finished entries. The first is on "Seasonal", the second is not on any list.
        /// AniList reports every list the user has defined on every entry, so the flag
        /// rather than the key's presence decides membership.
        /// </summary>
        private const string Json = @"{
            ""data"": { ""Page"": {
                ""pageInfo"": { ""currentPage"": 1, ""lastPage"": 1, ""hasNextPage"": false },
                ""mediaList"": [
                    { ""status"": ""COMPLETED"", ""progress"": 26,
                      ""customLists"": { ""Seasonal"": true, ""Rewatch"": false },
                      ""media"": { ""id"": 1, ""status"": ""FINISHED"", ""format"": ""TV"", ""episodes"": 26,
                                   ""title"": { ""userPreferred"": ""Cowboy Bebop"", ""romaji"": ""Cowboy Bebop"" } } },
                    { ""status"": ""COMPLETED"", ""progress"": 24,
                      ""customLists"": { ""Seasonal"": false, ""Rewatch"": false },
                      ""media"": { ""id"": 9253, ""status"": ""FINISHED"", ""format"": ""TV"", ""episodes"": 24,
                                   ""title"": { ""userPreferred"": ""Steins;Gate"", ""romaji"": ""Steins;Gate"" } } }
                ]
            } }
        }";

        private static AniListParser ParserWithCustomList(string customList)
        {
            return new AniListParser(new AniListSettings
            {
                Username = "someone",
                CustomList = customList,
                ImportFinished = true
            });
        }

        [Test]
        public void should_return_everything_when_no_custom_list_is_set()
        {
            var result = ParserWithCustomList(null).ParseResponse(Response(Json));

            result.Should().HaveCount(2);
        }

        [Test]
        public void should_return_everything_when_custom_list_is_blank()
        {
            var result = ParserWithCustomList("   ").ParseResponse(Response(Json));

            result.Should().HaveCount(2);
        }

        [Test]
        public void should_return_only_entries_on_the_named_custom_list()
        {
            var result = ParserWithCustomList("Seasonal").ParseResponse(Response(Json));

            result.Should().ContainSingle();
            result.Single().AniListId.Should().Be(1);
        }

        [Test]
        public void should_match_the_custom_list_name_case_insensitively()
        {
            // The name is typed by hand into settings, so its case cannot be relied upon.
            ParserWithCustomList("seasonal").ParseResponse(Response(Json)).Should().ContainSingle();
        }

        [Test]
        public void should_ignore_surrounding_whitespace_in_the_custom_list_name()
        {
            ParserWithCustomList("  Seasonal  ").ParseResponse(Response(Json)).Should().ContainSingle();
        }

        [Test]
        public void should_return_nothing_when_the_custom_list_matches_no_entries()
        {
            // A list that exists but holds nothing, and a name that does not exist at all,
            // must both yield an empty list rather than falling back to everything.
            ParserWithCustomList("Rewatch").ParseResponse(Response(Json)).Should().BeEmpty();
            ParserWithCustomList("Nonexistent").ParseResponse(Response(Json)).Should().BeEmpty();
        }

        [Test]
        public void should_exclude_entries_with_no_custom_lists_at_all()
        {
            var json = Json.Replace(@"""customLists"": { ""Seasonal"": true, ""Rewatch"": false },", string.Empty);

            ParserWithCustomList("Seasonal").ParseResponse(Response(json)).Should().BeEmpty();
        }
    }
}

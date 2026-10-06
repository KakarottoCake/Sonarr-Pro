using System.Text;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.ImportLists.Tmdb.Account;
using NzbDrone.Core.ImportLists.Tmdb.List;
using NzbDrone.Core.ImportLists.Tmdb.Person;

namespace NzbDrone.Core.Test.ImportListTests
{
    [TestFixture]
    public class TmdbParserFixture
    {
        private static ImportListResponse Response(string content)
        {
            var request = new HttpRequest("https://api.themoviedb.org/4/list/1");
            var response = new HttpResponse(request, new HttpHeader(), Encoding.UTF8.GetBytes(content));
            return new ImportListResponse(new ImportListRequest(request), response);
        }

        [Test]
        public void literal_json_preserves_tv_ids_and_excludes_movies_and_invalid_ids()
        {
            var response = Response("""
                {"results":[{"id":1399,"name":"Example Show","media_type":"tv"},{"id":9,"name":"Movie","media_type":"movie"},{"id":0,"name":"Unknown"}]}
                """);
            var result = new TmdbListParser().ParseResponse(response);
            result.Should().ContainSingle();
            result[0].TmdbId.Should().Be(1399);
            result[0].Title.Should().Be("Example Show");
        }

        [Test]
        public void person_credits_handle_missing_crew_and_filter_movies()
        {
            var response = Response("""
                {"cast":[{"id":1399,"name":"Example Show","media_type":"tv"},{"id":9,"media_type":"movie"}]}
                """);
            var result = new TmdbPersonParser(new TmdbPersonSettings { IncludingCastCredits = true }).ParseResponse(response);
            result.Should().ContainSingle();
            result[0].TmdbId.Should().Be(1399);
        }

        [Test]
        public void account_lists_require_an_account_id()
        {
            var settings = new TmdbAccountSettings { AuthToken = "example-token" };
            settings.Validate().IsValid.Should().BeFalse();
            settings.AccountId = "example-account-id";
            settings.Validate().IsValid.Should().BeTrue();
        }
    }
}

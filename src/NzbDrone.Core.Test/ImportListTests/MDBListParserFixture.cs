using System.Text;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.ImportLists;
using NzbDrone.Core.ImportLists.MDBList;

namespace NzbDrone.Core.Test.ImportListTests
{
    [TestFixture]
    public class MDBListParserFixture
    {
        [Test]
        public void parses_real_json_fields_and_excludes_movies()
        {
            var request = new HttpRequest("https://api.mdblist.com/lists/example/example/items/show");
            var content = """
                {"shows":[{"id":1399,"title":"Example Show","mediatype":"show","ids":{"tvdb":121361,"imdb":"tt0944947"}},{"id":9,"title":"Movie","mediatype":"movie"}]}
                """;
            var response = new HttpResponse(request, new HttpHeader(), Encoding.UTF8.GetBytes(content));
            var result = new MDBListParser().ParseResponse(new ImportListResponse(new ImportListRequest(request), response));
            result.Should().ContainSingle();
            result[0].TmdbId.Should().Be(1399);
            result[0].TvdbId.Should().Be(121361);
            result[0].ImdbId.Should().Be("tt0944947");
        }

        [Test]
        public void never_treats_negative_or_absent_ids_as_matches()
        {
            var request = new HttpRequest("https://api.mdblist.com/lists/example/example/items/show");
            var response = new HttpResponse(request, new HttpHeader(), Encoding.UTF8.GetBytes("[{\"id\":-7,\"tvdb_id\":-8,\"title\":\"Unknown\"}]"));
            var result = new MDBListParser().ParseResponse(new ImportListResponse(new ImportListRequest(request), response));
            result[0].TmdbId.Should().Be(0);
            result[0].TvdbId.Should().Be(0);
        }
    }
}

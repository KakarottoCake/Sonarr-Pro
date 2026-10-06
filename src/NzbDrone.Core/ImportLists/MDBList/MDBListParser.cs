using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json.Linq;
using NzbDrone.Core.ImportLists.Exceptions;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.ImportLists.MDBList
{
    public class MDBListParser : IParseImportListResponse
    {
        public IList<ImportListItemInfo> ParseResponse(ImportListResponse response)
        {
            if (response.HttpResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new ImportListException(response, "MDBList returned {0}. Check the API key and list access; rate limits return 429.", response.HttpResponse.StatusCode);
            }

            var document = JToken.Parse(response.Content);
            var items = document as JArray ?? document["shows"] as JArray ?? document["items"] as JArray;
            if (items == null)
            {
                throw new ImportListException(response, "MDBList did not return a show list.");
            }

            var result = new List<ImportListItemInfo>();
            foreach (var item in items)
            {
                var type = (string)item["mediatype"] ?? (string)item["type"];
                if (type != null && type != "show" && type != "tv")
                {
                    continue;
                }

                var ids = item["ids"];
                var tmdb = (string)ids?["tmdb"] ?? (string)item["tmdb_id"] ?? (string)item["tmdbid"] ?? (string)item["id"];
                var tvdb = (string)ids?["tvdb"] ?? (string)item["tvdb_id"];
                result.Add(new ImportListItemInfo
                {
                    Title = (string)item["title"] ?? (string)item["name"],
                    TmdbId = int.TryParse(tmdb, out var tmdbId) && tmdbId > 0 ? tmdbId : 0,
                    TvdbId = int.TryParse(tvdb, out var tvdbId) && tvdbId > 0 ? tvdbId : 0,
                    ImdbId = (string)ids?["imdb"] ?? (string)item["imdb_id"] ?? (string)item["imdbid"]
                });
            }

            return result;
        }
    }
}

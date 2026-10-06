using System.Collections.Generic;
using NzbDrone.Common.Http;

namespace NzbDrone.Core.ImportLists.MDBList
{
    public class MDBListRequestGenerator : IImportListRequestGenerator
    {
        private readonly MDBListSettings _settings;

        public MDBListRequestGenerator(MDBListSettings settings)
        {
            _settings = settings;
        }

        public ImportListPageableRequestChain GetListItems()
        {
            var chain = new ImportListPageableRequestChain();
            chain.Add(Requests());
            return chain;
        }

        private IEnumerable<ImportListRequest> Requests()
        {
            for (var offset = 0; offset < 1000; offset += 250)
            {
                var builder = new HttpRequestBuilder("https://api.mdblist.com")
                    .Resource(MDBListSettings.GetListPath(_settings.ListUrl))
                    .Accept(HttpAccept.Json)
                    .AddQueryParam("apikey", _settings.ApiKey)
                    .AddQueryParam("limit", 250)
                    .AddQueryParam("offset", offset);
                yield return new ImportListRequest(builder.Build());
            }
        }
    }
}

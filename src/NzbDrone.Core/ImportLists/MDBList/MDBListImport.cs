using System;
using System.Collections.Generic;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.ImportLists.MDBList
{
    public class MDBListImport : HttpImportListBase<MDBListSettings>
    {
        public MDBListImport(IHttpClient client,
            IImportListStatusService status,
            IConfigService config,
            IParsingService parsing,
            ILocalizationService localization,
            Logger logger)
            : base(client, status, config, parsing, localization, logger)
        {
        }

        public override string Name => "MDBList";
        public override ImportListType ListType => ImportListType.Other;
        public override TimeSpan MinRefreshInterval => TimeSpan.FromHours(6);
        public override int PageSize => 250;
        public override IParseImportListResponse GetParser() => new MDBListParser();
        public override IImportListRequestGenerator GetRequestGenerator() => new MDBListRequestGenerator(Settings);
        public override object RequestAction(string action, IDictionary<string, string> query) => new { };
    }
}

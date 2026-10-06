using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Localization;
using NzbDrone.Core.Parser;
using NzbDrone.Core.ThingiProvider;

namespace NzbDrone.Core.ImportLists.Tmdb;

public abstract class TmdbImportBase<TSettings> : HttpImportListBase<TSettings>
    where TSettings : TmdbSettingsBase<TSettings>, new()
{
    protected TmdbImportBase(IHttpClient httpClient,
        IImportListStatusService importListStatusService,
        IConfigService configService,
        IParsingService parsingService,
        ILocalizationService localizationService,
        Logger logger)
        : base(httpClient, importListStatusService, configService, parsingService, localizationService, logger)
    {
    }

    public override ImportListType ListType => ImportListType.Tmdb;
    public override TimeSpan MinRefreshInterval => TimeSpan.FromHours(6);
    public override IEnumerable<ProviderDefinition> DefaultDefinitions => GetPresetDefinitionPairs().Select(definition => new ImportListDefinition
    {
        Name = definition.Key, Settings = definition.Value, Implementation = GetType().Name
    });

    public override object RequestAction(string action, IDictionary<string, string> query) => new { };

    protected virtual IEnumerable<KeyValuePair<string, TSettings>> GetPresetDefinitionPairs() => [];
}

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using Sonarr.Http;
using Sonarr.Http.REST.Attributes;

namespace Sonarr.Api.V5.Settings;

[V5ApiController("settings/metadatasource")]
public class MetadataSourceSettingsController : SettingsController<MetadataSourceSettingsResource>
{
    private readonly IConfigService _configService;

    public MetadataSourceSettingsController(IConfigFileProvider configFileProvider, IConfigService configService)
        : base(configFileProvider, configService)
    {
        _configService = configService;
    }

    protected override MetadataSourceSettingsResource ToResource(IConfigFileProvider configFile, IConfigService model)
    {
        return MetadataSourceSettingsResourceMapper.ToResource(model);
    }

    /// <summary>
    /// Saves the key, unless what came back is the mask that stands in for the stored one.
    /// The base implementation writes every property it is given, which would otherwise
    /// overwrite a perfectly good key with a row of asterisks the moment anything else on
    /// the page is saved.
    /// </summary>
    [RestPutById]
    [Consumes("application/json")]
    [Produces("application/json")]
    public override Results<Accepted<MetadataSourceSettingsResource>, NotFound> SaveSettings([FromBody] MetadataSourceSettingsResource resource)
    {
        if (resource.TmdbApiKey != MetadataSourceSettingsResource.Mask)
        {
            _configService.TmdbApiKey = resource.TmdbApiKey.IsNullOrWhiteSpace()
                ? string.Empty
                : resource.TmdbApiKey.Trim();
        }

        return TypedAccepted(resource.Id);
    }
}

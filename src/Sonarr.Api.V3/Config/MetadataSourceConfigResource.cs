using NzbDrone.Core.Configuration;
using Sonarr.Http.REST;

namespace Sonarr.Api.V3.Config
{
    public class MetadataSourceConfigResource : RestResource
    {
        /// <summary>
        /// User-supplied TMDB API key. TMDB requires one per user and does not permit a
        /// shared key to be redistributed, so there is no default.
        /// </summary>
        public string TmdbApiKey { get; set; }
    }

    public static class MetadataSourceConfigResourceMapper
    {
        public static MetadataSourceConfigResource ToResource(IConfigService model)
        {
            return new MetadataSourceConfigResource
            {
                TmdbApiKey = model.TmdbApiKey
            };
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MetadataSource
{
    public interface IMetadataProviderFactory
    {
        IMetadataProvider GetProvider(Series series);
        IMetadataProvider GetProvider(MetadataSourceType source);
        IMetadataProvider DefaultProvider { get; }
        List<IMetadataProvider> All();
    }

    public class MetadataProviderFactory : IMetadataProviderFactory
    {
        public const MetadataSourceType DefaultSource = MetadataSourceType.Tvdb;

        private readonly IMetadataProvider[] _providers;
        private readonly Logger _logger;

        public MetadataProviderFactory(IEnumerable<IMetadataProvider> providers, Logger logger)
        {
            _providers = providers.ToArray();
            _logger = logger;
        }

        public IMetadataProvider DefaultProvider => GetProvider(DefaultSource);

        public List<IMetadataProvider> All()
        {
            return _providers.ToList();
        }

        public IMetadataProvider GetProvider(Series series)
        {
            return series == null ? DefaultProvider : GetProvider(series.MetadataSource);
        }

        public IMetadataProvider GetProvider(MetadataSourceType source)
        {
            var provider = _providers.FirstOrDefault(p => p.Source == source);

            if (provider != null)
            {
                return provider;
            }

            // A series can name a provider that is not registered, for instance if it was
            // added by a newer build. Falling back keeps the library usable rather than
            // failing every refresh for that series.
            var fallback = _providers.FirstOrDefault(p => p.Source == DefaultSource);

            if (fallback == null)
            {
                throw new MetadataProviderNotFoundException(source);
            }

            _logger.Warn("Metadata provider '{0}' is not registered, falling back to '{1}'", source, DefaultSource);

            return fallback;
        }
    }
}

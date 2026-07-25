using NzbDrone.Common.Exceptions;

namespace NzbDrone.Core.MetadataSource
{
    public class MetadataProviderNotFoundException : NzbDroneException
    {
        public MetadataSourceType RequestedSource { get; }

        public MetadataProviderNotFoundException(MetadataSourceType source)
            : base(string.Format("No metadata provider is registered for '{0}'", source))
        {
            RequestedSource = source;
        }
    }
}

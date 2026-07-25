using NzbDrone.Common.Exceptions;

namespace NzbDrone.Core.MetadataSource.Tmdb
{
    public class TmdbApiKeyMissingException : NzbDroneException
    {
        public TmdbApiKeyMissingException()
            : base("No TMDB API key is configured. Add one in Settings to use TMDB metadata.")
        {
        }

        public TmdbApiKeyMissingException(string message)
            : base(message)
        {
        }
    }
}

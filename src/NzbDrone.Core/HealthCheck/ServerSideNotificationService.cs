using NzbDrone.Core.Localization;

namespace NzbDrone.Core.HealthCheck
{
    /// <summary>
    /// Disabled in Sonarr Pro.
    ///
    /// Upstream sends version, OS, architecture and branch to services.sonarr.tv and renders
    /// whatever message comes back as a health warning. A fork's version string describes a build
    /// that service has never heard of, so any advice it returned would be about a different
    /// program — including prompts to "update" onto upstream Sonarr, which is exactly what this
    /// fork must not do. Skipping the call also keeps a fork's install data out of the Sonarr
    /// project's metrics.
    /// </summary>
    public class ServerSideNotificationService : HealthCheckBase
    {
        public ServerSideNotificationService(ILocalizationService localizationService)
            : base(localizationService)
        {
        }

        public override HealthCheck Check()
        {
            return new HealthCheck(GetType());
        }
    }
}

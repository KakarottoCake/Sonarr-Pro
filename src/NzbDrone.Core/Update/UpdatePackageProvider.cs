using System;
using System.Collections.Generic;

namespace NzbDrone.Core.Update
{
    public interface IUpdatePackageProvider
    {
        UpdatePackage GetLatestUpdate(string branch, Version currentVersion);
        List<UpdatePackage> GetRecentUpdates(string branch, Version currentVersion, Version previousVersion = null);
    }

    /// <summary>
    /// Sonarr Pro is distributed as a Docker image and updated with "docker pull", so there is
    /// no in-app updater to feed.
    ///
    /// Upstream asks services.sonarr.tv which build to install next. A fork must not do that: the
    /// versions it reports back describe upstream Sonarr, so anything offered would overwrite this
    /// build with a different program. The query also carries OS, architecture, runtime and
    /// database type, which is telemetry the Sonarr project has no reason to receive from a fork
    /// it does not maintain.
    ///
    /// Reporting "no update available" leaves the update UI, the scheduled check and the health
    /// check intact and quiet, rather than surfacing errors for a feature that is deliberately off.
    /// </summary>
    public class UpdatePackageProvider : IUpdatePackageProvider
    {
        public UpdatePackage GetLatestUpdate(string branch, Version currentVersion)
        {
            return null;
        }

        public List<UpdatePackage> GetRecentUpdates(string branch, Version currentVersion, Version previousVersion = null)
        {
            return new List<UpdatePackage>();
        }
    }
}

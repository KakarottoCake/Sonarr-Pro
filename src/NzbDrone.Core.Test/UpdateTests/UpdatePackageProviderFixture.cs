using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Common.Http;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Update;

namespace NzbDrone.Core.Test.UpdateTests
{
    /// <summary>
    /// Sonarr Pro never offers an in-app update. Upstream's version of this fixture asked
    /// services.sonarr.tv for real packages over the network; those tests are gone because the
    /// behaviour they covered is gone.
    /// </summary>
    public class UpdatePackageProviderFixture : CoreTest<UpdatePackageProvider>
    {
        [Test]
        public void should_never_offer_an_update()
        {
            Subject.GetLatestUpdate("main", new Version(3, 0)).Should().BeNull();
        }

        [Test]
        public void should_never_offer_an_update_for_an_unknown_branch()
        {
            Subject.GetLatestUpdate("invalid_branch", new Version(3, 0)).Should().BeNull();
        }

        [Test]
        public void should_report_no_recent_updates()
        {
            Subject.GetRecentUpdates("main", new Version(4, 0), null).Should().BeEmpty();
        }

        /// <summary>
        /// The point of the fork's provider is that it makes no request at all, so guard the
        /// dependency rather than the return value. Reintroducing an HTTP client here would put
        /// the call to services.sonarr.tv back, along with the OS and runtime details it carries.
        /// </summary>
        [Test]
        public void should_not_depend_on_an_http_client()
        {
            typeof(UpdatePackageProvider)
                .GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Should()
                .NotContain(p => p.ParameterType == typeof(IHttpClient));
        }
    }
}

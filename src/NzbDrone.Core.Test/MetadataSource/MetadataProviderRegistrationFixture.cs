using System;
using System.Linq;
using System.Runtime.CompilerServices;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.MetadataSource.SkyHook;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MetadataSource
{
    /// <summary>
    /// Services are auto-registered against every interface they implement, so a provider
    /// that implements one of the single-instance ports silently makes that resolution
    /// ambiguous at runtime. Unit tests elsewhere mock those ports and would not notice.
    /// </summary>
    [TestFixture]
    public class MetadataProviderRegistrationFixture : CoreTest
    {
        private static Type[] Implementations<T>()
        {
            return typeof(IMetadataProvider).Assembly
                .GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && typeof(T).IsAssignableFrom(t))
                .ToArray();
        }

        [Test]
        public void only_skyhook_should_implement_the_single_instance_series_info_port()
        {
            // RefreshSeriesService and AddSeriesService each inject one IProvideSeriesInfo.
            Implementations<IProvideSeriesInfo>().Should().Equal(typeof(SkyHookProxy));
        }

        [Test]
        public void only_skyhook_should_implement_the_single_instance_search_port()
        {
            // SeriesLookupController and ImportListSyncService each inject one.
            Implementations<ISearchForNewSeries>().Should().Equal(typeof(SkyHookProxy));
        }

        [Test]
        public void metadata_providers_should_not_implement_the_single_instance_ports()
        {
            var offenders = Implementations<IMetadataProvider>()
                .Where(t => t != typeof(SkyHookProxy))
                .Where(t => typeof(IProvideSeriesInfo).IsAssignableFrom(t) || typeof(ISearchForNewSeries).IsAssignableFrom(t))
                .ToArray();

            offenders.Should().BeEmpty("providers registered against the single-instance ports make their resolution ambiguous");
        }

        [Test]
        public void every_metadata_provider_should_report_a_distinct_source()
        {
            var sources = Implementations<IMetadataProvider>()
                .Select(t => (MetadataSourceType)t.GetProperty(nameof(IMetadataProvider.Source)).GetValue(RuntimeHelpers.GetUninitializedObject(t)))
                .ToArray();

            sources.Should().OnlyHaveUniqueItems("MetadataProviderFactory selects a provider by its source");
        }
    }
}

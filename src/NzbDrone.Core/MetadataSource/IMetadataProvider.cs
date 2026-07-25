using System;
using System.Collections.Generic;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MetadataSource
{
    /// <summary>
    /// A source of series and episode metadata.
    /// <para>
    /// Deliberately does not extend <see cref="IProvideSeriesInfo"/> or
    /// <see cref="ISearchForNewSeries"/>. Services are auto-registered against every
    /// interface they implement, and callers such as RefreshSeriesService and
    /// SeriesLookupController inject a single instance of those two. Inheriting them here
    /// would register every provider against them and make that resolution ambiguous.
    /// SkyHookProxy implements all three, so it remains the only candidate for the
    /// original pair and behaviour there is unchanged.
    /// </para>
    /// </summary>
    public interface IMetadataProvider
    {
        MetadataSourceType Source { get; }

        /// <summary>
        /// Fetch metadata using whichever identifier this provider keys on, taken from the
        /// series' <see cref="Series.ForeignId"/>.
        /// </summary>
        Tuple<Series, List<Episode>> GetSeriesInfo(Series series);

        List<Series> SearchForNewSeries(string title);
    }
}

using System;
using System.Collections.Generic;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MetadataSource
{
    /// <summary>
    /// A source of series and episode metadata.
    /// <para>
    /// Extends the pre-existing <see cref="IProvideSeriesInfo"/> and <see cref="ISearchForNewSeries"/>
    /// ports so that the original SkyHook implementation satisfies this contract unchanged,
    /// keeping the introduction of the abstraction behaviour-neutral.
    /// </para>
    /// </summary>
    public interface IMetadataProvider : IProvideSeriesInfo, ISearchForNewSeries
    {
        MetadataSourceType Source { get; }

        /// <summary>
        /// Fetch metadata for a series using whichever identifier this provider keys on.
        /// Preferred over <see cref="IProvideSeriesInfo.GetSeriesInfo(int)"/>, which assumes a TVDB id.
        /// </summary>
        Tuple<Series, List<Episode>> GetSeriesInfo(Series series);
    }
}

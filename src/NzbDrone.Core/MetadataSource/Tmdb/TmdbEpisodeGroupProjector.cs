using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.MetadataSource.Ordering;
using NzbDrone.Core.MetadataSource.Tmdb.Resource;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MetadataSource.Tmdb
{
    /// <summary>
    /// Renumbers episodes according to a TMDB episode group.
    /// <para>
    /// Kept free of HTTP and state so the renumbering can be tested directly: it is the
    /// part that decides what ends up in file names, and getting it wrong misfiles a library.
    /// </para>
    /// </summary>
    public static class TmdbEpisodeGroupProjector
    {
        /// <summary>
        /// Applies a group's ordering to episodes keyed by TMDB episode id.
        /// <para>
        /// Within the group, an entry's position (<c>Order</c>) gives the episode number and
        /// its containing group's position gives the season number. Episodes absent from the
        /// group keep their original numbering and are returned as specials-safe leftovers,
        /// so nothing is silently dropped from the library.
        /// </para>
        /// </summary>
        public static List<Episode> Project(TmdbEpisodeGroupDetailResource group, IEnumerable<Episode> episodes)
        {
            var byTmdbId = new Dictionary<int, Episode>();

            foreach (var episode in episodes)
            {
                // ForeignId holds the TMDB episode id, which is stable across orderings.
                if (int.TryParse(episode.ForeignId, out var tmdbId))
                {
                    byTmdbId[tmdbId] = episode;
                }
            }

            var projected = new List<Episode>();
            var claimed = new HashSet<int>();

            if (group?.Groups != null)
            {
                foreach (var item in group.Groups.OrderBy(g => g.Order))
                {
                    if (item.Episodes == null)
                    {
                        continue;
                    }

                    var episodeNumber = 1;

                    foreach (var entry in item.Episodes.OrderBy(e => e.Order))
                    {
                        if (!byTmdbId.TryGetValue(entry.Id, out var episode))
                        {
                            continue;
                        }

                        // An episode listed twice in one ordering would otherwise take two
                        // slots and shift everything after it.
                        if (!claimed.Add(entry.Id))
                        {
                            continue;
                        }

                        episode.SeasonNumber = item.Order;
                        episode.EpisodeNumber = episodeNumber++;

                        projected.Add(episode);
                    }
                }
            }

            // Anything the ordering does not mention keeps its provider numbering. Dropping
            // these would orphan any files already matched to them.
            foreach (var pair in byTmdbId)
            {
                if (!claimed.Contains(pair.Key))
                {
                    projected.Add(pair.Value);
                }
            }

            return projected;
        }

        public static EpisodeOrdering ToOrdering(TmdbEpisodeGroupSummaryResource summary)
        {
            return new EpisodeOrdering
            {
                Id = summary.Id,
                Name = summary.Name,
                Description = summary.Description,
                EpisodeCount = summary.EpisodeCount,
                SeasonCount = summary.GroupCount,
                IsAbsolute = summary.Type == (int)TmdbEpisodeGroupType.Absolute
            };
        }
    }
}

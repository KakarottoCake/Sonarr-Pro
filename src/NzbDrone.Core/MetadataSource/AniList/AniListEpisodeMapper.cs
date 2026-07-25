using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.MetadataSource.AniList.Resource;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.MetadataSource.AniList
{
    /// <summary>
    /// Builds an episode list from what AniList actually publishes.
    /// <para>
    /// AniList models an anime as one continuous run of episodes rather than as seasons:
    /// a sequel is a separate entry, not season 2. That is also how release groups number
    /// anime, so the entry maps to a single season numbered 1..N with matching absolute
    /// numbers, which is what filenames contain.
    /// </para>
    /// <para>
    /// AniList does not publish per-episode titles or overviews, so those are left null and
    /// the UI shows the episode as untitled. Air dates come from the airing schedule where
    /// it is available.
    /// </para>
    /// </summary>
    public static class AniListEpisodeMapper
    {
        public static List<Episode> MapEpisodes(AniListMediaResource media)
        {
            var airDates = BuildAirDateLookup(media);

            // While a series airs, AniList leaves the total null. The schedule then describes
            // more episodes than the declared count, so take whichever is larger to avoid
            // truncating the list mid-season.
            var count = Math.Max(media.Episodes ?? 0, airDates.Count == 0 ? 0 : airDates.Keys.Max());

            var episodes = new List<Episode>(count);

            for (var number = 1; number <= count; number++)
            {
                var episode = new Episode
                {
                    // AniList has no per-episode id, so the identity is scoped to the series
                    // entry. It stays stable as long as the numbering does, which is what
                    // keeps episode files attached.
                    ForeignId = string.Format("{0}:{1}", media.Id, number),
                    SeasonNumber = 1,
                    EpisodeNumber = number,
                    AbsoluteEpisodeNumber = number,
                    Monitored = true,
                    Images = new List<MediaCover.MediaCover>()
                };

                if (airDates.TryGetValue(number, out var airDate))
                {
                    episode.AirDate = airDate.ToString(Episode.AIR_DATE_FORMAT);
                    episode.AirDateUtc = airDate;
                }

                if (media.Duration.HasValue)
                {
                    episode.Runtime = media.Duration.Value;
                }

                episodes.Add(episode);
            }

            return episodes;
        }

        private static Dictionary<int, DateTime> BuildAirDateLookup(AniListMediaResource media)
        {
            var lookup = new Dictionary<int, DateTime>();

            var nodes = media.AiringSchedule?.Nodes;

            if (nodes == null)
            {
                return lookup;
            }

            foreach (var node in nodes.Where(n => n.Episode > 0))
            {
                lookup[node.Episode] = DateTimeOffset.FromUnixTimeSeconds(node.AiringAt).UtcDateTime;
            }

            return lookup;
        }

        /// <summary>
        /// Every name AniList knows for a series, most canonical first and deduplicated.
        /// Feeds release matching, where the alternative names matter more than the
        /// canonical one because they are what appears in filenames.
        /// </summary>
        public static List<string> GetAllTitles(AniListMediaResource media)
        {
            var titles = new List<string>();

            void Add(string title)
            {
                if (!string.IsNullOrWhiteSpace(title) && !titles.Contains(title, StringComparer.OrdinalIgnoreCase))
                {
                    titles.Add(title);
                }
            }

            Add(media.Title?.Romaji);
            Add(media.Title?.English);
            Add(media.Title?.UserPreferred);
            Add(media.Title?.Native);

            if (media.Synonyms != null)
            {
                foreach (var synonym in media.Synonyms)
                {
                    Add(synonym);
                }
            }

            return titles;
        }
    }
}

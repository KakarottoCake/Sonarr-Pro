using System;
using System.Linq;
using System.Text.RegularExpressions;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Parser
{
    public static class AnimePackMatcher
    {
        public static ParsedEpisodeInfo Match(string title, AnimeSeasonSearchCriteria criteria)
        {
            if (criteria.SeasonNumber <= 0 || criteria.CatalogueEpisodes == null || criteria.CatalogueEpisodes.Count == 0)
            {
                return null;
            }

            var episodes = criteria.CatalogueEpisodes.Where(e => e.SeasonNumber == criteria.SeasonNumber).OrderBy(e => e.EpisodeNumber).ToList();
            if (episodes.Count == 0 || !episodes.Select(e => e.EpisodeNumber).SequenceEqual(Enumerable.Range(1, episodes.Count)) ||
                episodes.Any(e => !e.AirDateUtc.HasValue || e.AirDateUtc.Value > DateTime.UtcNow))
            {
                return null;
            }

            var cleaned = Regex.Replace(title, @"^\s*\[[^\]]+\]\s*", string.Empty, RegexOptions.None, TimeSpan.FromSeconds(1));
            var normalized = Normalize(cleaned);
            var aliases = criteria.SeasonSceneTitles ?? [];
            if (criteria.Series.Seasons?.Count(s => s.SeasonNumber > 0) == 1)
            {
                aliases = aliases.Concat(criteria.SceneTitles ?? []).Append(criteria.Series.Title).ToList();
            }

            foreach (var alias in aliases.Where(a => !string.IsNullOrWhiteSpace(a)).OrderByDescending(a => a.Length))
            {
                var prefix = Normalize(alias);
                if (!normalized.StartsWith(prefix + " ", StringComparison.Ordinal))
                {
                    continue;
                }

                var suffix = normalized[(prefix.Length + 1)..];

                // A pack must explicitly cover the full known season. Specials, sequels,
                // partial ranges and bare catalogue titles retain the normal rejection.
                var range = Regex.Match(suffix, @"^0*1\s+0*(?<end>\d+)\s+(?:complete|batch)\b", RegexOptions.None, TimeSpan.FromSeconds(1));
                if (!range.Success || !int.TryParse(range.Groups["end"].Value, out var end) || end != episodes.Count ||
                    Regex.IsMatch(suffix, @"\b(?:ova|oad|specials?|movies?|part|season|s\d+)\b", RegexOptions.None, TimeSpan.FromSeconds(1)))
                {
                    continue;
                }

                return new ParsedEpisodeInfo
                {
                    SeriesTitle = criteria.Series.Title,
                    ReleaseTitle = title,
                    SeasonNumber = criteria.SeasonNumber,
                    FullSeason = true,
                    Quality = QualityParser.ParseQuality(title),
                    Languages = LanguageParser.ParseLanguages(title),
                    ReleaseGroup = ReleaseGroupParser.ParseReleaseGroup(title)
                };
            }

            return null;
        }

        private static string Normalize(string value) => Regex.Replace(value.ToLowerInvariant(), @"[^\p{L}\p{N}]+", " ", RegexOptions.None, TimeSpan.FromSeconds(1)).Trim();
    }
}

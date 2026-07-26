using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NzbDrone.Core.MetadataSource
{
    /// <summary>
    /// Scores how well a series title answers a search query.
    /// <para>
    /// Used only to rank lookup results, where a human picks from the list and a poor
    /// ranking costs nothing. It is deliberately not used to decide which series a release
    /// belongs to: that runs unattended, and a loose match there files episodes under the
    /// wrong series.
    /// </para>
    /// </summary>
    public static class SeriesTitleMatcher
    {
        private static readonly Regex PunctuationRegex = new Regex(@"[^\p{L}\p{Nd}\s]", RegexOptions.Compiled);
        private static readonly Regex WhitespaceRegex = new Regex(@"\s+", RegexOptions.Compiled);
        private static readonly Regex ArticleRegex = new Regex(@"^(a|an|the)\s+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// The many ways a season is written, particularly for anime, reduced to "season N".
        /// Without this "Attack on Titan 2nd Season" and "Attack on Titan Season 2" score as
        /// unrelated strings.
        /// </summary>
        private static readonly Regex SeasonSuffixRegex = new Regex(
            @"\b(?:season\s*(?<n>\d+)|(?<n>\d+)(?:st|nd|rd|th)\s*season|s(?<n>\d{1,2})|part\s*(?<n>\d+)|cour\s*(?<n>\d+))\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex RomanSeasonRegex = new Regex(
            @"\s(?<r>i{1,3}|iv|v|vi{1,3}|ix|x)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Dictionary<string, int> RomanNumerals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "i", 1 }, { "ii", 2 }, { "iii", 3 }, { "iv", 4 }, { "v", 5 },
            { "vi", 6 }, { "vii", 7 }, { "viii", 8 }, { "ix", 9 }, { "x", 10 }
        };

        /// <summary>
        /// Reduces a title to a form two spellings of the same series share: lower case,
        /// accents stripped, punctuation removed, leading article dropped, and any season
        /// marker written as "season N".
        /// </summary>
        public static string Normalize(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return string.Empty;
            }

            var value = RemoveDiacritics(title.ToLowerInvariant());

            value = PunctuationRegex.Replace(value, " ");
            value = WhitespaceRegex.Replace(value, " ").Trim();

            // Before the numeric forms, since "Ghost in the Shell II" ends in a numeral that
            // punctuation removal has already isolated.
            var roman = RomanSeasonRegex.Match(value);

            if (roman.Success && RomanNumerals.TryGetValue(roman.Groups["r"].Value, out var romanValue))
            {
                value = value.Substring(0, roman.Index) + " season " + romanValue;
            }

            value = SeasonSuffixRegex.Replace(value, m => "season " + m.Groups["n"].Value.TrimStart('0'));
            value = ArticleRegex.Replace(value, string.Empty);
            value = WhitespaceRegex.Replace(value, " ").Trim();

            return value;
        }

        /// <summary>
        /// Similarity of a query to a title, from 0 to 1.
        /// </summary>
        public static double Score(string query, string title)
        {
            var a = Normalize(query);
            var b = Normalize(title);

            if (a.Length == 0 || b.Length == 0)
            {
                return 0;
            }

            if (a == b)
            {
                return 1;
            }

            // A query that is a whole prefix of the title, as when someone types the first
            // few words, should rank above a merely similar string.
            if (b.StartsWith(a + " ", StringComparison.Ordinal))
            {
                return 0.95;
            }

            var characterScore = JaroWinkler(a, b);
            var wordScore = TokenSetRatio(a, b);

            // Whichever reading is kinder. Word overlap catches reordered or padded titles
            // that character similarity punishes; character similarity catches typos that
            // word overlap misses entirely.
            return Math.Max(characterScore, wordScore);
        }

        /// <summary>
        /// Proportion of query words that appear in the title. Rewards a query that is a
        /// subset of a longer title, which is what a partial search is.
        /// </summary>
        private static double TokenSetRatio(string a, string b)
        {
            var queryWords = a.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var titleWords = new HashSet<string>(b.Split(' ', StringSplitOptions.RemoveEmptyEntries));

            if (queryWords.Length == 0)
            {
                return 0;
            }

            var matched = queryWords.Count(titleWords.Contains);

            // Scaled by how much of the title went unused, so a one-word query does not score
            // full marks against a long title that merely contains it.
            var coverage = (double)matched / queryWords.Length;
            var precision = (double)matched / Math.Max(titleWords.Count, 1);

            return coverage * (0.7 + (0.3 * precision));
        }

        private static double JaroWinkler(string a, string b)
        {
            var jaro = Jaro(a, b);

            if (jaro < 0.7)
            {
                return jaro;
            }

            // Common prefixes matter disproportionately for titles, which are usually read
            // and typed from the start.
            var prefix = 0;

            while (prefix < Math.Min(4, Math.Min(a.Length, b.Length)) && a[prefix] == b[prefix])
            {
                prefix++;
            }

            return jaro + (prefix * 0.1 * (1 - jaro));
        }

        private static double Jaro(string a, string b)
        {
            var matchWindow = Math.Max((Math.Max(a.Length, b.Length) / 2) - 1, 0);

            var aMatched = new bool[a.Length];
            var bMatched = new bool[b.Length];
            var matches = 0;

            for (var i = 0; i < a.Length; i++)
            {
                var start = Math.Max(0, i - matchWindow);
                var end = Math.Min(i + matchWindow + 1, b.Length);

                for (var j = start; j < end; j++)
                {
                    if (bMatched[j] || a[i] != b[j])
                    {
                        continue;
                    }

                    aMatched[i] = true;
                    bMatched[j] = true;
                    matches++;
                    break;
                }
            }

            if (matches == 0)
            {
                return 0;
            }

            double transpositions = 0;
            var k = 0;

            for (var i = 0; i < a.Length; i++)
            {
                if (!aMatched[i])
                {
                    continue;
                }

                while (!bMatched[k])
                {
                    k++;
                }

                if (a[i] != b[k])
                {
                    transpositions++;
                }

                k++;
            }

            transpositions /= 2;

            return ((matches / (double)a.Length) + (matches / (double)b.Length) + ((matches - transpositions) / matches)) / 3;
        }

        private static string RemoveDiacritics(string value)
        {
            var normalized = value.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder(normalized.Length);

            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(c);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}

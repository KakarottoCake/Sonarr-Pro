using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;

namespace NzbDrone.Core.MetadataSource.Imdb
{
    /// <summary>
    /// Reads IMDb's gzipped TSV dumps a line at a time.
    /// <para>
    /// The episode dump alone is fifty megabytes compressed and several hundred expanded, so
    /// it is streamed rather than held in memory.
    /// </para>
    /// </summary>
    public static class ImdbTsvReader
    {
        /// <summary>
        /// Yields each row of title.episode.tsv already grouped by series, relying on the
        /// dump being ordered by episode id rather than by parent. Callers that need
        /// grouping must accumulate.
        /// </summary>
        public static IEnumerable<(int ParentTconst, ImdbEpisodeEntry Episode)> ReadEpisodes(Stream gzipped)
        {
            foreach (var fields in ReadRows(gzipped, 4))
            {
                var tconst = ParseTconst(fields[0]);
                var parent = ParseTconst(fields[1]);

                if (tconst == 0 || parent == 0)
                {
                    continue;
                }

                // "\N" is IMDb's null. An episode with neither number is still worth keeping,
                // since it exists and would otherwise vanish from the series.
                yield return (parent, new ImdbEpisodeEntry(tconst, ParseNumber(fields[2]), ParseNumber(fields[3])));
            }
        }

        private static IEnumerable<string[]> ReadRows(Stream gzipped, int expectedColumns)
        {
            using var decompressed = new GZipStream(gzipped, CompressionMode.Decompress);
            using var reader = new StreamReader(decompressed);

            // The first line is the header.
            reader.ReadLine();

            while (reader.ReadLine() is { } line)
            {
                var fields = line.Split('\t');

                if (fields.Length < expectedColumns)
                {
                    continue;
                }

                yield return fields;
            }
        }

        /// <summary>
        /// Converts "tt0959621" to 959621. The prefix is constant across the dataset, so
        /// only the digits are kept.
        /// </summary>
        public static int ParseTconst(string value)
        {
            if (value == null || value.Length < 3 || !value.StartsWith("tt", StringComparison.Ordinal))
            {
                return 0;
            }

            return int.TryParse(value.AsSpan(2), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        }

        private static int ParseNumber(string value)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        }
    }
}

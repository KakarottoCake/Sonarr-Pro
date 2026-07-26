using System;
using System.Collections.Generic;
using System.IO;

namespace NzbDrone.Core.MetadataSource.Imdb
{
    public readonly struct ImdbEpisodeEntry
    {
        public ImdbEpisodeEntry(int tconst, int seasonNumber, int episodeNumber)
        {
            Tconst = tconst;
            SeasonNumber = seasonNumber;
            EpisodeNumber = episodeNumber;
        }

        /// <summary>The numeric part of the episode's IMDb id, so tt0959621 is 959621.</summary>
        public int Tconst { get; }

        public int SeasonNumber { get; }

        public int EpisodeNumber { get; }
    }

    /// <summary>
    /// Packs a series' episode list into a compact blob.
    /// <para>
    /// IMDb publishes roughly nine million episode rows. Stored as text that is hundreds of
    /// megabytes, which is not worth carrying for what is only ever asked one question:
    /// "what episodes does this series have, and how are they numbered".
    /// </para>
    /// <para>
    /// Two things make it small. Episodes of one series were assigned adjacent ids, so the
    /// gap to the previous entry is stored rather than the id itself, and every value is
    /// written as a variable-length integer, which spends one byte on the small numbers that
    /// season and episode almost always are.
    /// </para>
    /// </summary>
    public static class ImdbEpisodePacker
    {
        public static byte[] Pack(IEnumerable<ImdbEpisodeEntry> episodes)
        {
            using var stream = new MemoryStream();

            var previousTconst = 0;

            foreach (var episode in episodes)
            {
                // Zig-zag encoded because ids are not always ascending, so a gap can be
                // negative, and a plain varint cannot represent that compactly.
                WriteVarint(stream, ZigZagEncode(episode.Tconst - previousTconst));
                WriteVarint(stream, (uint)Math.Max(episode.SeasonNumber, 0));
                WriteVarint(stream, (uint)Math.Max(episode.EpisodeNumber, 0));

                previousTconst = episode.Tconst;
            }

            return stream.ToArray();
        }

        public static List<ImdbEpisodeEntry> Unpack(byte[] packed)
        {
            var episodes = new List<ImdbEpisodeEntry>();

            if (packed == null || packed.Length == 0)
            {
                return episodes;
            }

            var offset = 0;
            var previousTconst = 0;

            while (offset < packed.Length)
            {
                var tconst = previousTconst + ZigZagDecode(ReadVarint(packed, ref offset));
                var season = (int)ReadVarint(packed, ref offset);
                var episode = (int)ReadVarint(packed, ref offset);

                episodes.Add(new ImdbEpisodeEntry(tconst, season, episode));

                previousTconst = tconst;
            }

            return episodes;
        }

        private static void WriteVarint(Stream stream, uint value)
        {
            while (value >= 0x80)
            {
                stream.WriteByte((byte)(value | 0x80));
                value >>= 7;
            }

            stream.WriteByte((byte)value);
        }

        private static uint ReadVarint(byte[] buffer, ref int offset)
        {
            uint value = 0;
            var shift = 0;

            while (offset < buffer.Length)
            {
                var current = buffer[offset++];

                value |= (uint)(current & 0x7F) << shift;

                if ((current & 0x80) == 0)
                {
                    return value;
                }

                shift += 7;
            }

            return value;
        }

        private static uint ZigZagEncode(int value)
        {
            return (uint)((value << 1) ^ (value >> 31));
        }

        private static int ZigZagDecode(uint value)
        {
            return (int)(value >> 1) ^ -(int)(value & 1);
        }
    }
}

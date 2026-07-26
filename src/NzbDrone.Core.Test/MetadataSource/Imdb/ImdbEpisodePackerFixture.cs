using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MetadataSource.Imdb;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MetadataSource.Imdb
{
    [TestFixture]
    public class ImdbEpisodePackerFixture : CoreTest
    {
        private static void RoundTrips(params ImdbEpisodeEntry[] episodes)
        {
            var unpacked = ImdbEpisodePacker.Unpack(ImdbEpisodePacker.Pack(episodes));

            unpacked.Should().HaveCount(episodes.Length);

            for (var i = 0; i < episodes.Length; i++)
            {
                unpacked[i].Tconst.Should().Be(episodes[i].Tconst);
                unpacked[i].SeasonNumber.Should().Be(episodes[i].SeasonNumber);
                unpacked[i].EpisodeNumber.Should().Be(episodes[i].EpisodeNumber);
            }
        }

        [Test]
        public void should_round_trip_a_single_episode()
        {
            RoundTrips(new ImdbEpisodeEntry(959621, 1, 1));
        }

        [Test]
        public void should_round_trip_consecutive_ids()
        {
            RoundTrips(
                new ImdbEpisodeEntry(959621, 1, 1),
                new ImdbEpisodeEntry(959622, 1, 2),
                new ImdbEpisodeEntry(959623, 1, 3));
        }

        [Test]
        public void should_round_trip_ids_that_go_backwards()
        {
            // Ids are not guaranteed ascending within a series, so the gap can be negative.
            RoundTrips(
                new ImdbEpisodeEntry(959700, 1, 1),
                new ImdbEpisodeEntry(959621, 1, 2),
                new ImdbEpisodeEntry(959800, 1, 3));
        }

        [Test]
        public void should_round_trip_large_ids_and_numbers()
        {
            RoundTrips(
                new ImdbEpisodeEntry(31000000, 25, 300),
                new ImdbEpisodeEntry(2, 0, 0));
        }

        [Test]
        public void should_round_trip_an_empty_list()
        {
            ImdbEpisodePacker.Pack(new List<ImdbEpisodeEntry>()).Should().BeEmpty();
            ImdbEpisodePacker.Unpack(new byte[0]).Should().BeEmpty();
            ImdbEpisodePacker.Unpack(null).Should().BeEmpty();
        }

        [Test]
        public void should_pack_a_typical_run_into_about_four_bytes_an_episode()
        {
            // The point of the format. Text would be roughly thirty bytes a row, which is
            // what makes the raw dataset too large to keep.
            var episodes = Enumerable.Range(0, 1000)
                                     .Select(i => new ImdbEpisodeEntry(959621 + i, (i / 25) + 1, (i % 25) + 1))
                                     .ToList();

            var packed = ImdbEpisodePacker.Pack(episodes);

            packed.Length.Should().BeLessThan(4000);
            ImdbEpisodePacker.Unpack(packed).Should().HaveCount(1000);
        }
    }

    [TestFixture]
    public class ImdbTsvReaderFixture : CoreTest
    {
        private static Stream Gzip(string content)
        {
            var output = new MemoryStream();

            using (var gzip = new GZipStream(output, CompressionMode.Compress, true))
            using (var writer = new StreamWriter(gzip, Encoding.UTF8))
            {
                writer.Write(content);
            }

            output.Position = 0;

            return output;
        }

        [TestCase("tt0959621", 959621)]
        [TestCase("tt0000001", 1)]
        [TestCase("tt31000000", 31000000)]
        [TestCase("nm0000001", 0)]
        [TestCase("garbage", 0)]
        [TestCase("", 0)]
        [TestCase(null, 0)]
        public void should_read_the_numeric_part_of_an_imdb_id(string value, int expected)
        {
            ImdbTsvReader.ParseTconst(value).Should().Be(expected);
        }

        [Test]
        public void should_read_episodes_and_skip_the_header()
        {
            var tsv = "tconst\tparentTconst\tseasonNumber\tepisodeNumber\n" +
                      "tt0959621\ttt0903747\t1\t1\n" +
                      "tt0959622\ttt0903747\t1\t2\n";

            var rows = ImdbTsvReader.ReadEpisodes(Gzip(tsv)).ToList();

            rows.Should().HaveCount(2);
            rows[0].ParentTconst.Should().Be(903747);
            rows[0].Episode.Tconst.Should().Be(959621);
            rows[0].Episode.SeasonNumber.Should().Be(1);
            rows[1].Episode.EpisodeNumber.Should().Be(2);
        }

        [Test]
        public void should_keep_episodes_with_no_numbering()
        {
            // IMDb writes an unknown value as \N. Such an episode still exists, and dropping
            // it would silently shorten the series.
            var tsv = "tconst\tparentTconst\tseasonNumber\tepisodeNumber\n" +
                      "tt0959621\ttt0903747\t\\N\t\\N\n";

            var rows = ImdbTsvReader.ReadEpisodes(Gzip(tsv)).ToList();

            rows.Should().ContainSingle();
            rows[0].Episode.SeasonNumber.Should().Be(0);
        }

        [Test]
        public void should_skip_rows_with_an_unusable_id()
        {
            var tsv = "tconst\tparentTconst\tseasonNumber\tepisodeNumber\n" +
                      "bad\ttt0903747\t1\t1\n" +
                      "tt0959621\tbad\t1\t1\n" +
                      "tt0959622\ttt0903747\t1\t2\n";

            ImdbTsvReader.ReadEpisodes(Gzip(tsv)).Should().ContainSingle();
        }
    }
}

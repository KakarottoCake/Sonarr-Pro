using System;
using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration
{
    [TestFixture]
    public class PreserveSourceReleaseContextFixture : MigrationTest<preserve_source_release_context>
    {
        [Test]
        public void backfills_only_explicit_matching_import_and_grab_records()
        {
            var date = new DateTime(2020, 1, 1);
            var db = WithMigrationTestDb(m =>
            {
                foreach (var id in new[] { 10, 11 })
                {
                    m.Insert.IntoTable("EpisodeFiles").Row(new
                    {
                        Id = id, SeriesId = 7, RelativePath = $"episode-{id}.mkv", Quality = "{}", Size = 100L,
                        DateAdded = date, SeasonNumber = 1, Languages = "[]"
                    });
                }

                m.Insert.IntoTable("History").Row(new
                {
                    Id = 1, SeriesId = 7, EpisodeId = 1, SourceTitle = "Example.Show.S01.COMPLETE.1080p", Date = date,
                    Quality = "{}", Languages = "[]", EventType = 1, DownloadId = "torrent", Data = "{\"Size\":\"12000000000\"}"
                });
                m.Insert.IntoTable("History").Row(new
                {
                    Id = 2, SeriesId = 7, EpisodeId = 1, SourceTitle = "episode-10", Date = date.AddMinutes(1),
                    Quality = "{}", Languages = "[]", EventType = 3, DownloadId = "torrent", Data = "{\"FileId\":\"10\"}"
                });
            });
            var rows = db.Query<SourceContextRow>("SELECT \"Id\", \"Size\", \"SourceReleaseSize\", \"SourceReleaseTitle\" FROM \"EpisodeFiles\" ORDER BY \"Id\"").ToList();
            rows[0].SourceReleaseSize.Should().Be(12000000000);
            rows[0].SourceReleaseTitle.Should().Be("Example.Show.S01.COMPLETE.1080p");
            rows[1].SourceReleaseSize.Should().BeNull();
            rows.Should().OnlyContain(r => r.Size == 100);
        }
    }

    public class SourceContextRow
    {
        public int Id { get; set; }
        public long Size { get; set; }
        public long? SourceReleaseSize { get; set; }
        public string SourceReleaseTitle { get; set; }
    }
}

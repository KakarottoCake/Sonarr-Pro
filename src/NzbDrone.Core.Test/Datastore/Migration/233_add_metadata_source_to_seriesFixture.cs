using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration;

[TestFixture]
public class add_metadata_source_to_seriesFixture : MigrationTest<add_metadata_source_to_series>
{
    [Test]
    public void should_default_existing_series_to_tvdb_source()
    {
        var db = WithMigrationTestDb(c =>
        {
            c.Insert.IntoTable("Series").Row(SeriesRow(tvdbId: 73255));
        });

        var series = db.Query<Series233>("SELECT \"Id\", \"TvdbId\", \"MetadataSource\", \"ForeignId\" FROM \"Series\"");

        series.Should().HaveCount(1);
        series.First().MetadataSource.Should().Be(0);
    }

    [Test]
    public void should_backfill_foreign_id_from_tvdb_id()
    {
        var db = WithMigrationTestDb(c =>
        {
            c.Insert.IntoTable("Series").Row(SeriesRow(tvdbId: 73255));
        });

        var series = db.Query<Series233>("SELECT \"Id\", \"TvdbId\", \"MetadataSource\", \"ForeignId\" FROM \"Series\"");

        series.First().ForeignId.Should().Be("73255");
    }

    [Test]
    public void should_leave_foreign_id_null_when_no_tvdb_id()
    {
        var db = WithMigrationTestDb(c =>
        {
            c.Insert.IntoTable("Series").Row(SeriesRow(tvdbId: 0));
        });

        var series = db.Query<Series233>("SELECT \"Id\", \"TvdbId\", \"MetadataSource\", \"ForeignId\" FROM \"Series\"");

        series.First().ForeignId.Should().BeNull();
    }

    private static object SeriesRow(int tvdbId)
    {
        return new
        {
            TvdbId = tvdbId,
            TvRageId = 0,
            TvMazeId = 0,
            Title = "Battlestar Galactica",
            CleanTitle = "battlestargalactica",
            Status = 0,
            Images = "[]",
            Path = "/tv/Battlestar Galactica " + tvdbId,
            Monitored = true,
            SeasonFolder = true,
            Runtime = 45,
            SeriesType = 0,
            UseSceneNumbering = false,
            TitleSlug = "battlestar-galactica-" + tvdbId,
            Seasons = "[]",
            Genres = "[]",
            Tags = "[]",
            Actors = "[]",
            QualityProfileId = 1,
            OriginalLanguage = 1,
            Added = "2020-01-01 00:00:00"
        };
    }
}

internal class Series233
{
    public int Id { get; set; }
    public int TvdbId { get; set; }
    public int MetadataSource { get; set; }
    public string ForeignId { get; set; }
}

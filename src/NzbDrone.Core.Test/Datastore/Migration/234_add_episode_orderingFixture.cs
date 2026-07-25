using System.Linq;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Datastore.Migration;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Datastore.Migration;

[TestFixture]
public class add_episode_orderingFixture : MigrationTest<add_episode_ordering>
{
    [Test]
    public void should_default_existing_series_to_the_providers_default_ordering()
    {
        var db = WithMigrationTestDb(c =>
        {
            c.Insert.IntoTable("Series").Row(SeriesRow());
        });

        var series = db.Query<Series234>("SELECT \"Id\", \"OrderingId\" FROM \"Series\"");

        series.Should().HaveCount(1);
        series.First().OrderingId.Should().BeNull();
    }

    [Test]
    public void should_backfill_episode_foreign_id_from_tvdb_id()
    {
        var db = WithMigrationTestDb(c =>
        {
            c.Insert.IntoTable("Series").Row(SeriesRow());
            c.Insert.IntoTable("Episodes").Row(EpisodeRow(tvdbId: 4567891));
        });

        var episodes = db.Query<Episode234>("SELECT \"Id\", \"TvdbId\", \"ForeignId\" FROM \"Episodes\"");

        episodes.Should().HaveCount(1);
        episodes.First().ForeignId.Should().Be("4567891");
    }

    [Test]
    public void should_leave_episode_foreign_id_null_when_no_tvdb_id()
    {
        var db = WithMigrationTestDb(c =>
        {
            c.Insert.IntoTable("Series").Row(SeriesRow());
            c.Insert.IntoTable("Episodes").Row(EpisodeRow(tvdbId: 0));
        });

        var episodes = db.Query<Episode234>("SELECT \"Id\", \"TvdbId\", \"ForeignId\" FROM \"Episodes\"");

        episodes.First().ForeignId.Should().BeNull();
    }

    private static object SeriesRow()
    {
        return new
        {
            TvdbId = 73255,
            TvRageId = 0,
            TvMazeId = 0,
            Title = "Battlestar Galactica",
            CleanTitle = "battlestargalactica",
            Status = 0,
            Images = "[]",
            Path = "/tv/Battlestar Galactica",
            Monitored = true,
            SeasonFolder = true,
            Runtime = 45,
            SeriesType = 0,
            UseSceneNumbering = false,
            TitleSlug = "battlestar-galactica",
            Seasons = "[]",
            Genres = "[]",
            Tags = "[]",
            Actors = "[]",
            QualityProfileId = 1,
            OriginalLanguage = 1,
            Added = "2020-01-01 00:00:00"
        };
    }

    private static object EpisodeRow(int tvdbId)
    {
        return new
        {
            SeriesId = 1,
            TvdbId = tvdbId,
            EpisodeFileId = 0,
            SeasonNumber = 1,
            EpisodeNumber = 1,
            Title = "33",
            Overview = "",
            Monitored = true,
            Runtime = 45,
            Images = "[]",
            UnverifiedSceneNumbering = false
        };
    }
}

internal class Series234
{
    public int Id { get; set; }
    public string OrderingId { get; set; }
}

internal class Episode234
{
    public int Id { get; set; }
    public int TvdbId { get; set; }
    public string ForeignId { get; set; }
}

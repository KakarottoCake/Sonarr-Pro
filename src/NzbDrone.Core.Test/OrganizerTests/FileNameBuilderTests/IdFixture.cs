using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.OrganizerTests.FileNameBuilderTests
{
    [TestFixture]
    public class IdFixture : CoreTest<FileNameBuilder>
    {
        private Series _series;
        private NamingConfig _namingConfig;

        [SetUp]
        public void Setup()
        {
            _series = Builder<Series>
                      .CreateNew()
                      .With(s => s.Title = "Series Title")
                      .With(s => s.ImdbId = "tt12345")
                      .With(s => s.TvdbId = 12345)
                      .With(s => s.TvRageId = 54321)
                      .Build();

            _namingConfig = NamingConfig.Default;

            Mocker.GetMock<INamingConfigService>()
                  .Setup(c => c.GetConfig()).Returns(_namingConfig);
        }

        [Test]
        public void should_add_imdb_id()
        {
            _namingConfig.SeriesFolderFormat = "{Series Title} ({ImdbId})";

            Subject.GetSeriesFolder(_series)
                   .Should().Be($"Series Title ({_series.ImdbId})");
        }

        [Test]
        public void should_add_tvdb_id()
        {
            _namingConfig.SeriesFolderFormat = "{Series Title} ({TvdbId})";

            Subject.GetSeriesFolder(_series)
                   .Should().Be($"Series Title ({_series.TvdbId})");
        }

        [Test]
        public void should_add_tvmaze_id()
        {
            _namingConfig.SeriesFolderFormat = "{Series Title} ({TvMazeId})";

            Subject.GetSeriesFolder(_series)
                   .Should().Be($"Series Title ({_series.TvMazeId})");
        }

        [Test]
        public void should_add_tmdb_id()
        {
            _namingConfig.SeriesFolderFormat = "{Series Title} ({TmdbId})";

            Subject.GetSeriesFolder(_series)
                .Should().Be($"Series Title ({_series.TmdbId})");
        }

        [TestCase(MetadataSourceType.Tvdb, 72244, 3122, "tt0361243", "{tvdb-72244}")]
        [TestCase(MetadataSourceType.Tmdb, 72244, 3122, "tt0361243", "{tmdb-3122}")]
        [TestCase(MetadataSourceType.Tmdb, 72244, 0, "tt0361243", "{tvdb-72244}")]
        [TestCase(MetadataSourceType.AniList, -123, 3122, "tt0361243", "{tmdb-3122}")]
        [TestCase(MetadataSourceType.MyAnimeList, 0, 0, "tt0361243", "{imdb-tt0361243}")]
        [TestCase(MetadataSourceType.AniList, -123, 0, null, "")]
        [TestCase(MetadataSourceType.AniList, -123, -1, "invalid", "")]
        public void plex_id_should_use_a_valid_matching_provider(MetadataSourceType source, int tvdbId, int tmdbId, string imdbId, string expected)
        {
            _series.Title = "Star Wars: Clone Wars";
            _series.Year = 2003;
            _series.MetadataSource = source;
            _series.TvdbId = tvdbId;
            _series.TmdbId = tmdbId;
            _series.ImdbId = imdbId;
            _namingConfig.SeriesFolderFormat = "{Series TitleYear} {Plex Id}";

            Subject.GetSeriesFolder(_series)
                .Should().Be($"Star Wars - Clone Wars (2003) {expected}".TrimEnd());
        }

        [TestCase(0)]
        [TestCase(-123)]
        public void should_not_include_placeholder_tvdb_ids(int tvdbId)
        {
            _series.TvdbId = tvdbId;
            _namingConfig.SeriesFolderFormat = "{Series Title} {TvdbId}";

            Subject.GetSeriesFolder(_series).Should().Be("Series Title");
        }

        [Test]
        public void plex_folder_should_not_duplicate_an_existing_year()
        {
            _series.Title = "Star Wars: Clone Wars (2003)";
            _series.Year = 2003;
            _series.TvdbId = 72244;
            _series.MetadataSource = MetadataSourceType.Tvdb;
            _namingConfig.SeriesFolderFormat = "{Series TitleYear} {Plex Id}";

            Subject.GetSeriesFolder(_series).Should().Be("Star Wars - Clone Wars (2003) {tvdb-72244}");
        }
    }
}

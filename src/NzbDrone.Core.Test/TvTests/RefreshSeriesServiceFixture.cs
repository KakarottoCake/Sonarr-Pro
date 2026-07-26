using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FizzWare.NBuilder;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.AutoTagging;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Core.Tv.Commands;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.TvTests
{
    [TestFixture]
    public class RefreshSeriesServiceFixture : CoreTest<RefreshSeriesService>
    {
        private Series _series;
        private Mock<IMetadataProvider> _metadataProvider;

        [SetUp]
        public void Setup()
        {
            var season1 = Builder<Season>.CreateNew()
                                         .With(s => s.SeasonNumber = 1)
                                         .Build();

            _series = Builder<Series>.CreateNew()
                                     .With(s => s.Status = SeriesStatusType.Continuing)
                                     .With(s => s.Seasons = new List<Season>
                                                            {
                                                                season1
                                                            })
                                     .Build();

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.GetSeries(_series.Id))
                  .Returns(_series);

            _metadataProvider = new Mock<IMetadataProvider>();

            _metadataProvider.Setup(s => s.GetSeriesInfo(It.IsAny<Series>()))
                             .Callback<Series>(p => { throw new SeriesNotFoundException(p.TvdbId); });

            Mocker.GetMock<IMetadataProviderFactory>()
                  .Setup(s => s.GetProvider(It.IsAny<Series>()))
                  .Returns(() => _metadataProvider.Object);

            Mocker.GetMock<IAutoTaggingService>()
                .Setup(s => s.GetTagChanges(_series))
                .Returns(new AutoTaggingChanges());
        }

        private void GivenNewSeriesInfo(Series series)
        {
            _metadataProvider.Setup(s => s.GetSeriesInfo(It.Is<Series>(v => v.TvdbId == _series.TvdbId)))
                             .Returns(new Tuple<Series, List<Episode>>(series, new List<Episode>()));
        }

        [Test]
        public void should_monitor_new_seasons_automatically_if_monitor_new_items_is_all()
        {
            _series.MonitorNewItems = NewItemMonitorTypes.All;

            var newSeriesInfo = _series.JsonClone();
            newSeriesInfo.Seasons.Add(Builder<Season>.CreateNew()
                                         .With(s => s.SeasonNumber = 2)
                                         .Build());

            GivenNewSeriesInfo(newSeriesInfo);

            Subject.Execute(new RefreshSeriesCommand(new List<int> { _series.Id }));

            Mocker.GetMock<ISeriesService>()
                .Verify(v => v.UpdateSeries(It.Is<Series>(s => s.Seasons.Count == 2 && s.Seasons.Single(season => season.SeasonNumber == 2).Monitored == true), It.IsAny<bool>(), It.IsAny<bool>()));
        }

        [Test]
        public void should_not_monitor_new_seasons_automatically_if_monitor_new_items_is_none()
        {
            _series.MonitorNewItems = NewItemMonitorTypes.None;

            var newSeriesInfo = _series.JsonClone();
            newSeriesInfo.Seasons.Add(Builder<Season>.CreateNew()
                .With(s => s.SeasonNumber = 2)
                .Build());

            GivenNewSeriesInfo(newSeriesInfo);

            Subject.Execute(new RefreshSeriesCommand(new List<int> { _series.Id }));

            Mocker.GetMock<ISeriesService>()
                .Verify(v => v.UpdateSeries(It.Is<Series>(s => s.Seasons.Count == 2 && s.Seasons.Single(season => season.SeasonNumber == 2).Monitored == false), It.IsAny<bool>(), It.IsAny<bool>()));
        }

        [Test]
        public void should_not_monitor_new_special_season_automatically()
        {
            var series = _series.JsonClone();
            series.Seasons.Add(Builder<Season>.CreateNew()
                                         .With(s => s.SeasonNumber = 0)
                                         .Build());

            GivenNewSeriesInfo(series);

            Subject.Execute(new RefreshSeriesCommand(new List<int> { _series.Id }));

            Mocker.GetMock<ISeriesService>()
                .Verify(v => v.UpdateSeries(It.Is<Series>(s => s.Seasons.Count == 2 && s.Seasons.Single(season => season.SeasonNumber == 0).Monitored == false), It.IsAny<bool>(), It.IsAny<bool>()));
        }

        [Test]
        public void should_update_tvrage_id_if_changed()
        {
            var newSeriesInfo = _series.JsonClone();
            newSeriesInfo.TvRageId = _series.TvRageId + 1;

            GivenNewSeriesInfo(newSeriesInfo);

            Subject.Execute(new RefreshSeriesCommand(new List<int> { _series.Id }));

            Mocker.GetMock<ISeriesService>()
                .Verify(v => v.UpdateSeries(It.Is<Series>(s => s.TvRageId == newSeriesInfo.TvRageId), It.IsAny<bool>(), It.IsAny<bool>()));
        }

        [Test]
        public void should_update_tvmaze_id_if_changed()
        {
            var newSeriesInfo = _series.JsonClone();
            newSeriesInfo.TvMazeId = _series.TvMazeId + 1;

            GivenNewSeriesInfo(newSeriesInfo);

            Subject.Execute(new RefreshSeriesCommand(new List<int> { _series.Id }));

            Mocker.GetMock<ISeriesService>()
                .Verify(v => v.UpdateSeries(It.Is<Series>(s => s.TvMazeId == newSeriesInfo.TvMazeId), It.IsAny<bool>(), It.IsAny<bool>()));
        }

        [Test]
        public void should_update_tmdb_id_if_changed()
        {
            var newSeriesInfo = _series.JsonClone();
            newSeriesInfo.TmdbId = _series.TmdbId + 1;

            GivenNewSeriesInfo(newSeriesInfo);

            Subject.Execute(new RefreshSeriesCommand(new List<int> { _series.Id }));

            Mocker.GetMock<ISeriesService>()
                .Verify(v => v.UpdateSeries(It.Is<Series>(s => s.TmdbId == newSeriesInfo.TmdbId), It.IsAny<bool>(), It.IsAny<bool>()));
        }

        [Test]
        public void should_log_error_if_tvdb_id_not_found()
        {
            Subject.Execute(new RefreshSeriesCommand(new List<int> { _series.Id }));

            Mocker.GetMock<ISeriesService>()
                .Verify(v => v.UpdateSeries(It.Is<Series>(s => s.Status == SeriesStatusType.Deleted), It.IsAny<bool>(), It.IsAny<bool>()), Times.Once());

            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_mark_as_deleted_if_tvdb_id_not_found()
        {
            Subject.Execute(new RefreshSeriesCommand(new List<int> { _series.Id }));

            Mocker.GetMock<ISeriesService>()
                .Verify(v => v.UpdateSeries(It.Is<Series>(s => s.Status == SeriesStatusType.Deleted), It.IsAny<bool>(), It.IsAny<bool>()), Times.Once());

            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_not_remark_as_deleted_if_tvdb_id_not_found()
        {
            _series.Status = SeriesStatusType.Deleted;

            Subject.Execute(new RefreshSeriesCommand(new List<int> { _series.Id }));

            Mocker.GetMock<ISeriesService>()
                .Verify(v => v.UpdateSeries(It.IsAny<Series>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never());

            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_update_if_tvdb_id_changed()
        {
            var newSeriesInfo = _series.JsonClone();
            newSeriesInfo.TvdbId = _series.TvdbId + 1;

            GivenNewSeriesInfo(newSeriesInfo);

            Subject.Execute(new RefreshSeriesCommand(new List<int> { _series.Id }));

            Mocker.GetMock<ISeriesService>()
                .Verify(v => v.UpdateSeries(It.Is<Series>(s => s.TvdbId == newSeriesInfo.TvdbId), It.IsAny<bool>(), It.IsAny<bool>()));

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_not_throw_if_duplicate_season_is_in_existing_info()
        {
            var newSeriesInfo = _series.JsonClone();
            newSeriesInfo.Seasons.Add(Builder<Season>.CreateNew()
                                         .With(s => s.SeasonNumber = 2)
                                         .Build());

            _series.Seasons.Add(Builder<Season>.CreateNew()
                                         .With(s => s.SeasonNumber = 2)
                                         .Build());

            _series.Seasons.Add(Builder<Season>.CreateNew()
                                         .With(s => s.SeasonNumber = 2)
                                         .Build());

            GivenNewSeriesInfo(newSeriesInfo);

            Subject.Execute(new RefreshSeriesCommand(new List<int> { _series.Id }));

            Mocker.GetMock<ISeriesService>()
                  .Verify(v => v.UpdateSeries(It.Is<Series>(s => s.Seasons.Count == 2), It.IsAny<bool>(), It.IsAny<bool>()));
        }

        [Test]
        public void should_filter_duplicate_seasons()
        {
            var newSeriesInfo = _series.JsonClone();
            newSeriesInfo.Seasons.Add(Builder<Season>.CreateNew()
                                         .With(s => s.SeasonNumber = 2)
                                         .Build());

            newSeriesInfo.Seasons.Add(Builder<Season>.CreateNew()
                                         .With(s => s.SeasonNumber = 2)
                                         .Build());

            GivenNewSeriesInfo(newSeriesInfo);

            Subject.Execute(new RefreshSeriesCommand(new List<int> { _series.Id }));

            Mocker.GetMock<ISeriesService>()
                  .Verify(v => v.UpdateSeries(It.Is<Series>(s => s.Seasons.Count == 2), It.IsAny<bool>(), It.IsAny<bool>()));
        }

        [Test]
        public void should_keep_a_synthetic_tvdb_id_when_the_provider_reports_none()
        {
            // A series with no TheTVDB entry holds a negative placeholder id. Providers that
            // do not map to TheTVDB report zero, and taking that would wipe the placeholder
            // and break the unique-index invariant on the column.
            _series.TvdbId = -1;

            var newSeriesInfo = _series.JsonClone();
            newSeriesInfo.TvdbId = 0;

            GivenNewSeriesInfo(newSeriesInfo);

            Subject.Execute(new RefreshSeriesCommand(new List<int> { _series.Id }));

            Mocker.GetMock<ISeriesService>()
                  .Verify(v => v.UpdateSeries(It.Is<Series>(s => s.TvdbId == -1), It.IsAny<bool>(), It.IsAny<bool>()));
        }

        [Test]
        public void should_take_a_real_tvdb_id_when_the_provider_resolves_one()
        {
            _series.TvdbId = 100;

            var newSeriesInfo = _series.JsonClone();
            newSeriesInfo.TvdbId = 200;

            GivenNewSeriesInfo(newSeriesInfo);

            Subject.Execute(new RefreshSeriesCommand(new List<int> { _series.Id }));

            Mocker.GetMock<ISeriesService>()
                  .Verify(v => v.UpdateSeries(It.Is<Series>(s => s.TvdbId == 200), It.IsAny<bool>(), It.IsAny<bool>()));

            ExceptionVerification.ExpectedWarns(1);
        }

        [Test]
        public void should_rescan_series_if_updating_fails()
        {
            _metadataProvider.Setup(s => s.GetSeriesInfo(It.IsAny<Series>()))
                             .Throws(new IOException());

            Subject.Execute(new RefreshSeriesCommand(new List<int> { _series.Id }));

            Mocker.GetMock<IDiskScanService>()
                  .Verify(v => v.Scan(_series), Times.Once());

            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_not_rescan_series_if_updating_fails_with_series_not_found()
        {
            _metadataProvider.Setup(s => s.GetSeriesInfo(It.IsAny<Series>()))
                             .Throws(new SeriesNotFoundException(_series.Id));

            Subject.Execute(new RefreshSeriesCommand(new List<int> { _series.Id }));

            Mocker.GetMock<IDiskScanService>()
                  .Verify(v => v.Scan(_series), Times.Never());

            ExceptionVerification.ExpectedErrors(1);
        }
    }
}

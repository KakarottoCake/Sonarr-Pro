using System;
using System.Collections.Generic;
using System.IO;
using FizzWare.NBuilder;
using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Organizer;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.TvTests
{
    [TestFixture]
    public class AddSeriesFixture : CoreTest<AddSeriesService>
    {
        private Series _fakeSeries;
        private Mock<IMetadataProvider> _metadataProvider;

        [SetUp]
        public void Setup()
        {
            _fakeSeries = Builder<Series>
                .CreateNew()
                .With(s => s.Path = null)
                .Build();

            _metadataProvider = new Mock<IMetadataProvider>();

            Mocker.GetMock<IMetadataProviderFactory>()
                  .Setup(s => s.GetProvider(It.IsAny<Series>()))
                  .Returns(() => _metadataProvider.Object);
        }

        private void GivenValidSeries(int tvdbId)
        {
            _metadataProvider.Setup(s => s.GetSeriesInfo(It.Is<Series>(v => v.TvdbId == tvdbId)))
                             .Returns(new Tuple<Series, List<Episode>>(_fakeSeries, new List<Episode>()));
        }

        private void GivenValidPath()
        {
            Mocker.GetMock<IBuildFileNames>()
                  .Setup(s => s.GetSeriesFolder(It.IsAny<Series>(), null))
                  .Returns<Series, NamingConfig>((c, n) => c.Title);

            Mocker.GetMock<IAddSeriesValidator>()
                  .Setup(s => s.Validate(It.IsAny<Series>()))
                  .Returns(new ValidationResult());
        }

        [Test]
        public void should_be_able_to_add_a_series_without_passing_in_title()
        {
            var newSeries = new Series
            {
                TvdbId = 1,
                RootFolderPath = @"C:\Test\TV"
            };

            GivenValidSeries(newSeries.TvdbId);
            GivenValidPath();

            var series = Subject.AddSeries(newSeries);

            series.Title.Should().Be(_fakeSeries.Title);
        }

        [Test]
        public void should_have_proper_path()
        {
            var newSeries = new Series
                            {
                                TvdbId = 1,
                                RootFolderPath = @"C:\Test\TV"
                            };

            GivenValidSeries(newSeries.TvdbId);
            GivenValidPath();

            var series = Subject.AddSeries(newSeries);

            series.Path.Should().Be(Path.Combine(newSeries.RootFolderPath, _fakeSeries.Title));
        }

        [Test]
        public void should_throw_if_series_validation_fails()
        {
            var newSeries = new Series
            {
                TvdbId = 1,
                Path = @"C:\Test\TV\Title1"
            };

            GivenValidSeries(newSeries.TvdbId);

            Mocker.GetMock<IAddSeriesValidator>()
                  .Setup(s => s.Validate(It.IsAny<Series>()))
                  .Returns(new ValidationResult(new List<ValidationFailure>
                                                {
                                                    new ValidationFailure("Path", "Test validation failure")
                                                }));

            Assert.Throws<ValidationException>(() => Subject.AddSeries(newSeries));
        }

        [Test]
        public void should_throw_if_series_cannot_be_found()
        {
            var newSeries = new Series
            {
                TvdbId = 1,
                Path = @"C:\Test\TV\Title1"
            };

            _metadataProvider.Setup(s => s.GetSeriesInfo(It.IsAny<Series>()))
                             .Throws(new SeriesNotFoundException(newSeries.TvdbId));

            Mocker.GetMock<IAddSeriesValidator>()
                  .Setup(s => s.Validate(It.IsAny<Series>()))
                  .Returns(new ValidationResult(new List<ValidationFailure>
                                                {
                                                    new ValidationFailure("Path", "Test validation failure")
                                                }));

            Assert.Throws<ValidationException>(() => Subject.AddSeries(newSeries));

            ExceptionVerification.ExpectedErrors(1);
        }

        [Test]
        public void should_use_the_provider_named_by_the_request()
        {
            GivenValidSeries(1);
            GivenValidPath();

            var newSeries = new Series
            {
                TvdbId = 1,
                MetadataSource = MetadataSourceType.Tmdb,
                ForeignId = "456",
                RootFolderPath = @"C:\Test\TV"
            };

            Subject.AddSeries(newSeries);

            Mocker.GetMock<IMetadataProviderFactory>()
                  .Verify(v => v.GetProvider(It.Is<Series>(s => s.MetadataSource == MetadataSourceType.Tmdb)), Times.Once());
        }

        [Test]
        public void should_keep_the_chosen_ordering()
        {
            GivenValidSeries(1);
            GivenValidPath();

            // The ordering is the user's choice, not the provider's, and is fixed once the
            // series exists because it determines the numbering written into file names.
            var newSeries = new Series
            {
                TvdbId = 1,
                OrderingId = "absolute-group",
                RootFolderPath = @"C:\Test\TV"
            };

            Subject.AddSeries(newSeries);

            Mocker.GetMock<ISeriesService>()
                  .Verify(v => v.AddSeries(It.Is<Series>(s => s.OrderingId == "absolute-group")), Times.Once());
        }

        [Test]
        public void should_allocate_a_synthetic_id_when_the_series_is_not_on_tvdb()
        {
            // Content TheTVDB folds into a parent series still needs a distinct value, since
            // the column is uniquely indexed and lookups expect a single match.
            _fakeSeries.TvdbId = 0;

            _metadataProvider.Setup(s => s.GetSeriesInfo(It.IsAny<Series>()))
                             .Returns(new Tuple<Series, List<Episode>>(_fakeSeries, new List<Episode>()));

            Mocker.GetMock<IAllocateSyntheticSeriesIds>()
                  .Setup(s => s.AllocateTvdbId())
                  .Returns(-7);

            GivenValidPath();

            Subject.AddSeries(new Series
            {
                TvdbId = 0,
                MetadataSource = MetadataSourceType.AniList,
                ForeignId = "21",
                RootFolderPath = @"C:\Test\TV"
            });

            Mocker.GetMock<ISeriesService>()
                  .Verify(v => v.AddSeries(It.Is<Series>(s => s.TvdbId == -7)), Times.Once());
        }

        [Test]
        public void should_not_allocate_a_synthetic_id_when_the_provider_resolved_a_real_one()
        {
            _fakeSeries.TvdbId = 73255;

            _metadataProvider.Setup(s => s.GetSeriesInfo(It.IsAny<Series>()))
                             .Returns(new Tuple<Series, List<Episode>>(_fakeSeries, new List<Episode>()));

            GivenValidPath();

            Subject.AddSeries(new Series
            {
                TvdbId = 0,
                MetadataSource = MetadataSourceType.Tmdb,
                ForeignId = "456",
                RootFolderPath = @"C:\Test\TV"
            });

            Mocker.GetMock<IAllocateSyntheticSeriesIds>()
                  .Verify(v => v.AllocateTvdbId(), Times.Never());
        }

        [Test]
        public void should_keep_the_id_resolved_by_the_provider_when_the_request_has_none()
        {
            // A series added from TMDB or AniList arrives without a TVDB id. The provider may
            // still resolve one, and it must survive rather than being zeroed by the request.
            _fakeSeries.TvdbId = 73255;

            _metadataProvider.Setup(s => s.GetSeriesInfo(It.IsAny<Series>()))
                             .Returns(new Tuple<Series, List<Episode>>(_fakeSeries, new List<Episode>()));

            GivenValidPath();

            Subject.AddSeries(new Series
            {
                TvdbId = 0,
                MetadataSource = MetadataSourceType.Tmdb,
                ForeignId = "456",
                RootFolderPath = @"C:\Test\TV"
            });

            Mocker.GetMock<ISeriesService>()
                  .Verify(v => v.AddSeries(It.Is<Series>(s => s.TvdbId == 73255)), Times.Once());
        }
    }
}

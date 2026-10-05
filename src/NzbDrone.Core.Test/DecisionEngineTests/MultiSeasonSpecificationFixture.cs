using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class MultiSeasonSpecificationFixture : CoreTest<MultiSeasonSpecification>
    {
        private RemoteEpisode _remoteEpisode;

        [SetUp]
        public void Setup()
        {
            var series = Builder<Series>.CreateNew().With(s => s.Id = 1234).Build();
            _remoteEpisode = new RemoteEpisode
            {
                ParsedEpisodeInfo = new ParsedEpisodeInfo
                {
                    FullSeason = true,
                    SeasonNumbers = new[] { 1, 2, 3, 4, 5 }
                },
                Episodes = Builder<Episode>.CreateListOfSize(3)
                                           .All()
                                           .With(s => s.SeriesId = series.Id)
                                           .BuildList(),
                Series = series,
                Release = new ReleaseInfo
                {
                    Title = "Series.Title.S01-05.720p.BluRay.X264-RlsGrp"
                }
            };

            GivenMultiSeasonEnabled(true);
        }

        private void GivenMultiSeasonEnabled(bool enabled)
        {
            Mocker.GetMock<IConfigService>()
                  .SetupGet(s => s.EnableMultiSeasonReleases)
                  .Returns(enabled);
        }

        [Test]
        public void should_return_true_if_is_not_a_multi_season_release()
        {
            _remoteEpisode.ParsedEpisodeInfo.SeasonNumbers = new[] { 1 };
            _remoteEpisode.Episodes.Last().AirDateUtc = DateTime.UtcNow.AddDays(+2);
            Subject.IsSatisfiedBy(_remoteEpisode, new()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_false_if_multi_season_releases_are_disabled()
        {
            GivenMultiSeasonEnabled(false);

            Subject.IsSatisfiedBy(_remoteEpisode, new()).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_return_true_if_multi_season_release_matches_wanted_episodes()
        {
            Subject.IsSatisfiedBy(_remoteEpisode, new()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_false_if_multi_season_release_matches_no_episodes()
        {
            // Title alone is not enough. Without this a pack spanning seasons 1-9 would be
            // accepted for a season 3 search even though nothing in it was mapped.
            _remoteEpisode.Episodes = new List<Episode>();

            Subject.IsSatisfiedBy(_remoteEpisode, new()).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_return_false_if_multi_season_release_has_null_episodes()
        {
            _remoteEpisode.Episodes = null;

            Subject.IsSatisfiedBy(_remoteEpisode, new()).Accepted.Should().BeFalse();
        }
    }
}

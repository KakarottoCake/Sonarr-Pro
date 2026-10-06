using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.LibraryTools;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.LibraryTools
{
    [TestFixture]
    public class ExternalIdsFixture : CoreTest<ExternalIdsSpecification>
    {
        [TestCase(1, 2, true, false)]
        [TestCase(1, 2, false, true)]
        [TestCase(-1, 2, true, true)]
        [TestCase(1, 0, true, true)]
        [TestCase(1, 1, true, true)]
        public void only_rejects_positive_conflicting_ids_when_enabled(int seriesId, int releaseId, bool enabled, bool accepted)
        {
            Mocker.GetMock<IProOptionsService>().Setup(o => o.Read()).Returns(new ProOptions { MatchExternalIds = enabled });
            var episode = new RemoteEpisode { Series = new Series { TvdbId = seriesId }, Release = new ReleaseInfo { TvdbId = releaseId } };
            Subject.IsSatisfiedBy(episode, null).Accepted.Should().Be(accepted);
        }

        [TestCase("tt0001234", "1234", true)]
        [TestCase("tt1234", "tt2345", false)]
        [TestCase("tt1234", null, true)]
        public void normalizes_imdb_ids_and_allows_missing_ids(string seriesId, string releaseId, bool accepted)
        {
            Mocker.GetMock<IProOptionsService>().Setup(o => o.Read()).Returns(new ProOptions { MatchExternalIds = true });
            var episode = new RemoteEpisode { Series = new Series { ImdbId = seriesId }, Release = new ReleaseInfo { ImdbId = releaseId } };
            Subject.IsSatisfiedBy(episode, null).Accepted.Should().Be(accepted);
        }
    }
}

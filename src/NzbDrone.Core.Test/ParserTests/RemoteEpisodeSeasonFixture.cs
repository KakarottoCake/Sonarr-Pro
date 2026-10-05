using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.ParserTests
{
    [TestFixture]
    public class RemoteEpisodeSeasonFixture
    {
        [Test]
        public void should_preserve_torrent_information_and_cached_episodes_when_selecting_a_season()
        {
            var release = new TorrentInfo { InfoHash = "hash", MagnetUrl = "magnet:?xt=urn:btih:hash" };
            var remote = new RemoteEpisode
            {
                Release = release,
                Episodes = new List<Episode> { new Episode { Id = 101, SeasonNumber = 1 }, new Episode { Id = 301, SeasonNumber = 3 } }
            };

            var scoped = remote.ForSeason(3);

            scoped.Episodes.Should().ContainSingle().Which.Id.Should().Be(301);
            scoped.Release.Should().BeSameAs(release);
            remote.Episodes.Should().HaveCount(2);
        }
    }
}

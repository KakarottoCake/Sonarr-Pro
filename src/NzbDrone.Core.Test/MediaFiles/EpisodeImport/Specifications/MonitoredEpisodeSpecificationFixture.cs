using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download;
using NzbDrone.Core.MediaFiles.EpisodeImport.Specifications;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.MediaFiles.EpisodeImport.Specifications
{
    [TestFixture]
    public class MonitoredEpisodeSpecificationFixture : CoreTest<MonitoredEpisodeSpecification>
    {
        private LocalEpisode _localEpisode;
        private DownloadClientItem _downloadClientItem;

        [SetUp]
        public void Setup()
        {
            var series = Builder<Series>.CreateNew().With(s => s.Monitored = true).Build();

            _localEpisode = new LocalEpisode
            {
                Path = @"C:\Test\Series.Title.S01E01.mkv",
                Series = series,
                Episodes = Builder<Episode>.CreateListOfSize(1).All().With(e => e.Monitored = true).BuildList()
            };

            _downloadClientItem = new DownloadClientItem();

            GivenSkipEnabled(true);
        }

        private void GivenSkipEnabled(bool enabled)
        {
            Mocker.GetMock<IConfigService>()
                  .SetupGet(s => s.SkipUnmonitoredEpisodesFromPacks)
                  .Returns(enabled);
        }

        private void GivenEpisodesMonitored(params bool[] monitored)
        {
            _localEpisode.Episodes = monitored
                .Select((m, i) => new Episode { Id = i + 1, EpisodeNumber = i + 1, Monitored = m })
                .ToList();
        }

        [Test]
        public void should_accept_a_monitored_episode()
        {
            Subject.IsSatisfiedBy(_localEpisode, _downloadClientItem).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_reject_an_unmonitored_episode()
        {
            GivenEpisodesMonitored(false);

            Subject.IsSatisfiedBy(_localEpisode, _downloadClientItem).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_accept_a_multi_episode_file_if_any_episode_is_monitored()
        {
            // The file has to be imported to get the wanted episode, so the unwanted one
            // comes along unavoidably.
            GivenEpisodesMonitored(false, true);

            Subject.IsSatisfiedBy(_localEpisode, _downloadClientItem).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_when_the_setting_is_off()
        {
            GivenSkipEnabled(false);
            GivenEpisodesMonitored(false);

            Subject.IsSatisfiedBy(_localEpisode, _downloadClientItem).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_a_manual_import_regardless()
        {
            // A manual import has no download client item. The user picked the file, so that
            // outranks the monitored flag.
            GivenEpisodesMonitored(false);

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_when_the_series_itself_is_unmonitored()
        {
            // Every episode of an unmonitored series is unmonitored, so the check would
            // reject the whole import rather than trimming a pack.
            _localEpisode.Series.Monitored = false;
            GivenEpisodesMonitored(false);

            Subject.IsSatisfiedBy(_localEpisode, _downloadClientItem).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_when_no_episodes_were_matched()
        {
            _localEpisode.Episodes = new List<Episode>();

            Subject.IsSatisfiedBy(_localEpisode, _downloadClientItem).Accepted.Should().BeTrue();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using FizzWare.NBuilder;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.History;
using NzbDrone.Core.MediaFiles.EpisodeImport.Specifications;
using NzbDrone.Core.MediaFiles.MediaInfo;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.MediaFiles.EpisodeImport.Specifications
{
    [TestFixture]
    public class FullSeasonSpecificationFixture : CoreTest<FullSeasonSpecification>
    {
        private LocalEpisode _localEpisode;

        [SetUp]
        public void Setup()
        {
            _localEpisode = new LocalEpisode
            {
                Path = @"C:\Test\30 Rock\30.rock.s01e01.avi".AsOsAgnostic(),
                Size = 100,
                Series = Builder<Series>.CreateNew().Build(),
                FileEpisodeInfo = new ParsedEpisodeInfo
                                    {
                                        FullSeason = false
                                    }
            };
        }

        [Test]
        public void should_return_true_if_no_fileinfo_available()
        {
            _localEpisode.FileEpisodeInfo = null;
            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_return_false_when_file_contains_the_full_season()
        {
            _localEpisode.FileEpisodeInfo.FullSeason = true;

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_return_true_when_file_does_not_contain_the_full_season()
        {
            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeTrue();
        }

        [TestCase(60, true)]
        [TestCase(20, false)]
        [TestCase(0, false)]
        public void should_require_season_runtime_for_a_combined_video_in_a_multi_season_pack(int minutes, bool accepted)
        {
            GivenWholeSeasonInMultiSeasonPack();
            _localEpisode.MediaInfo.RunTime = TimeSpan.FromMinutes(minutes);

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().Be(accepted);
        }

        [Test]
        public void should_reject_combined_season_video_without_grab_history()
        {
            GivenWholeSeasonInMultiSeasonPack();
            _localEpisode.Release = null;

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_reject_combined_season_video_if_not_all_episodes_were_requested()
        {
            GivenWholeSeasonInMultiSeasonPack();
            _localEpisode.Release.EpisodeIds.RemoveAt(0);

            Subject.IsSatisfiedBy(_localEpisode, null).Accepted.Should().BeFalse();
        }

        private void GivenWholeSeasonInMultiSeasonPack()
        {
            _localEpisode.FileEpisodeInfo = new ParsedEpisodeInfo { FullSeason = true, SeasonNumber = 3 };
            _localEpisode.DownloadClientEpisodeInfo = new ParsedEpisodeInfo { FullSeason = true, SeasonNumbers = new[] { 1, 2, 3 } };
            _localEpisode.Episodes = Enumerable.Range(1, 5).Select(id => new Episode { Id = id, SeasonNumber = 3, EpisodeNumber = id, Runtime = 12 }).ToList();
            _localEpisode.MediaInfo = new MediaInfoModel { RunTime = TimeSpan.FromMinutes(60) };
            _localEpisode.Release = new GrabbedReleaseInfo(new List<EpisodeHistory>
            {
                new EpisodeHistory { EpisodeId = 1 }
            });
            _localEpisode.Release.EpisodeIds = _localEpisode.Episodes.Select(episode => episode.Id).ToList();
        }
    }
}

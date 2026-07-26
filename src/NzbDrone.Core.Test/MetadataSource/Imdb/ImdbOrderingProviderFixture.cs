using System.Collections.Generic;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.MetadataSource.Imdb;
using NzbDrone.Core.MetadataSource.Imdb.Commands;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.MetadataSource.Imdb
{
    [TestFixture]
    public class ImdbOrderingProviderFixture : CoreTest<ImdbOrderingProvider>
    {
        private void GivenIndexHolds(params ImdbEpisodeEntry[] episodes)
        {
            Mocker.GetMock<IImdbIndex>()
                  .Setup(s => s.GetEpisodes(It.IsAny<int>()))
                  .Returns(new List<ImdbEpisodeEntry>(episodes));
        }

        private void GivenScheduledRefreshDue(bool due)
        {
            Mocker.GetMock<IImdbDatasetService>()
                  .Setup(s => s.ShouldRefreshOnSchedule())
                  .Returns(due);
        }

        private void VerifyBuilt(Times times)
        {
            Mocker.GetMock<IImdbDatasetService>().Verify(v => v.BuildIndex(), times);
        }

        [Test]
        public void should_build_when_a_person_asked_for_it()
        {
            // Run from System, Tasks or the API. The index may not exist yet, which is
            // exactly when someone would ask.
            GivenScheduledRefreshDue(false);

            Subject.Execute(new RefreshImdbDatasetCommand { Trigger = CommandTrigger.Manual });

            VerifyBuilt(Times.Once());
        }

        [Test]
        public void should_not_download_on_a_schedule_when_nothing_is_due()
        {
            // The daily check must not spring a fifty megabyte download on someone who never
            // asked for this feature.
            GivenScheduledRefreshDue(false);

            Subject.Execute(new RefreshImdbDatasetCommand { Trigger = CommandTrigger.Scheduled });

            VerifyBuilt(Times.Never());
        }

        [Test]
        public void should_refresh_on_a_schedule_when_an_existing_index_is_stale()
        {
            GivenScheduledRefreshDue(true);

            Subject.Execute(new RefreshImdbDatasetCommand { Trigger = CommandTrigger.Scheduled });

            VerifyBuilt(Times.Once());
        }

        [Test]
        public void should_treat_an_unspecified_trigger_as_automatic()
        {
            // Anything not explicitly a person acting is treated as automatic, so a new
            // trigger source cannot accidentally bypass the guard.
            GivenScheduledRefreshDue(false);

            Subject.Execute(new RefreshImdbDatasetCommand { Trigger = CommandTrigger.Unspecified });

            VerifyBuilt(Times.Never());
        }

        [Test]
        public void should_offer_no_ordering_without_an_imdb_id()
        {
            Subject.GetOrdering(null).Should().BeNull();
            Subject.GetOrdering("").Should().BeNull();
            Subject.GetOrdering("not-an-id").Should().BeNull();
        }

        [Test]
        public void should_offer_no_ordering_when_the_index_holds_nothing()
        {
            // So the picker shows no choice rather than an empty one.
            GivenIndexHolds();

            Subject.GetOrdering("tt0903747").Should().BeNull();
        }

        [Test]
        public void should_describe_the_ordering_it_holds()
        {
            GivenIndexHolds(
                new ImdbEpisodeEntry(1, 1, 1),
                new ImdbEpisodeEntry(2, 1, 2),
                new ImdbEpisodeEntry(3, 2, 1));

            var ordering = Subject.GetOrdering("tt0903747");

            ordering.Should().NotBeNull();
            ordering.EpisodeCount.Should().Be(3);
            ordering.SeasonCount.Should().Be(2);
            ordering.IsAbsolute.Should().BeFalse();
        }

        [Test]
        public void should_renumber_episodes_it_recognises()
        {
            GivenIndexHolds(new ImdbEpisodeEntry(959621, 3, 7));

            var episodes = new List<Episode>
            {
                new Episode { ForeignId = "tt0959621", SeasonNumber = 1, EpisodeNumber = 1 }
            };

            var result = Subject.ApplyOrdering("tt0903747", episodes);

            result[0].SeasonNumber.Should().Be(3);
            result[0].EpisodeNumber.Should().Be(7);
        }

        [Test]
        public void should_leave_episodes_it_does_not_recognise_alone()
        {
            // Dropping or renumbering these would orphan any file already matched to them.
            GivenIndexHolds(new ImdbEpisodeEntry(959621, 3, 7));

            var episodes = new List<Episode>
            {
                new Episode { ForeignId = "tt0999999", SeasonNumber = 1, EpisodeNumber = 1 },
                new Episode { ForeignId = null, SeasonNumber = 2, EpisodeNumber = 2 }
            };

            var result = Subject.ApplyOrdering("tt0903747", episodes);

            result.Should().HaveCount(2);
            result[0].SeasonNumber.Should().Be(1);
            result[1].SeasonNumber.Should().Be(2);
        }

        [Test]
        public void should_keep_existing_numbering_when_the_index_holds_nothing_for_the_series()
        {
            GivenIndexHolds();

            var episodes = new List<Episode>
            {
                new Episode { ForeignId = "tt0959621", SeasonNumber = 1, EpisodeNumber = 1 }
            };

            Subject.ApplyOrdering("tt0903747", episodes)[0].EpisodeNumber.Should().Be(1);
        }
    }
}

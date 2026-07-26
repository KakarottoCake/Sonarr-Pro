using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Core.Tv;

namespace NzbDrone.Core.Test.TvTests
{
    [TestFixture]
    public class SyntheticSeriesIdServiceFixture : CoreTest<SyntheticSeriesIdService>
    {
        private void GivenExistingTvdbIds(params int[] ids)
        {
            var lookup = new Dictionary<int, int>();

            for (var i = 0; i < ids.Length; i++)
            {
                lookup[i + 1] = ids[i];
            }

            Mocker.GetMock<ISeriesService>()
                  .Setup(s => s.AllSeriesTvdbIds())
                  .Returns(lookup);
        }

        [Test]
        public void should_start_at_minus_one_for_an_empty_library()
        {
            GivenExistingTvdbIds();

            Subject.AllocateTvdbId().Should().Be(-1);
        }

        [Test]
        public void should_start_at_minus_one_when_every_series_has_a_real_id()
        {
            GivenExistingTvdbIds(73255, 81797, 121361);

            Subject.AllocateTvdbId().Should().Be(-1);
        }

        [Test]
        public void should_continue_below_the_lowest_synthetic_id()
        {
            GivenExistingTvdbIds(73255, -1, -2);

            Subject.AllocateTvdbId().Should().Be(-3);
        }

        [Test]
        public void should_not_reuse_an_id_that_is_still_in_use()
        {
            // Reuse would collide with the unique index on the column, and would attach the
            // new series to whatever the existing one already has.
            GivenExistingTvdbIds(-1, -2, -3);

            Subject.AllocateTvdbId().Should().Be(-4);
        }

        [Test]
        public void should_stay_negative_when_a_real_id_is_zero()
        {
            // Zero means "unknown" elsewhere, so it must never be handed out as an id.
            GivenExistingTvdbIds(0, 73255);

            Subject.AllocateTvdbId().Should().Be(-1);
        }
    }
}

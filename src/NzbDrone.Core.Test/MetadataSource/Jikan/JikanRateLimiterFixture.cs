using System.Diagnostics;
using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.MetadataSource.Jikan;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.MetadataSource.Jikan
{
    [TestFixture]
    public class JikanRateLimiterFixture : CoreTest
    {
        [Test]
        public void should_not_delay_the_first_burst()
        {
            var limiter = new JikanRateLimiter();
            var stopwatch = Stopwatch.StartNew();

            // Three per second is allowed, so this burst should pass straight through.
            limiter.WaitForSlot();
            limiter.WaitForSlot();
            limiter.WaitForSlot();

            stopwatch.Stop();
            stopwatch.ElapsedMilliseconds.Should().BeLessThan(200);
        }

        [Test]
        public void should_delay_the_fourth_request_within_a_second()
        {
            var limiter = new JikanRateLimiter();

            limiter.WaitForSlot();
            limiter.WaitForSlot();
            limiter.WaitForSlot();

            var stopwatch = Stopwatch.StartNew();
            limiter.WaitForSlot();
            stopwatch.Stop();

            // Exceeding three per second must wait for the window to roll, not fail.
            stopwatch.ElapsedMilliseconds.Should().BeGreaterThan(500);
        }

        [Test]
        public void should_keep_admitting_requests_after_the_window_rolls()
        {
            var limiter = new JikanRateLimiter();

            for (var i = 0; i < 6; i++)
            {
                limiter.WaitForSlot();
            }

            // Six requests fit inside the per-minute allowance, so all must be admitted
            // rather than the limiter deadlocking once the per-second window fills.
            Assert.Pass();
        }
    }
}

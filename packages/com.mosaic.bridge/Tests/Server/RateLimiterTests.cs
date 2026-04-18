using System.Threading;
using Mosaic.Bridge.Core.Server;
using NUnit.Framework;

namespace Mosaic.Bridge.Tests.Server
{
    [TestFixture]
    public class RateLimiterTests
    {
        [Test]
        public void TryConsume_UnderLimit_ReturnsTrue()
        {
            var limiter = new RateLimiter(100);

            var result = limiter.TryConsume("client-a");

            Assert.IsTrue(result);
        }

        [Test]
        public void TryConsume_AtLimit_ReturnsFalse()
        {
            const int max = 10;
            var limiter = new RateLimiter(max);

            // Consume all tokens
            for (int i = 0; i < max; i++)
            {
                Assert.IsTrue(limiter.TryConsume("client-a"),
                    $"Request {i + 1} should succeed");
            }

            // Next request should be rejected
            Assert.IsFalse(limiter.TryConsume("client-a"),
                "Request beyond limit should be rejected");
        }

        [Test]
        public void TryConsume_RefillsOverTime()
        {
            const int max = 5;
            var limiter = new RateLimiter(max);

            // Exhaust all tokens
            for (int i = 0; i < max; i++)
                limiter.TryConsume("client-a");

            Assert.IsFalse(limiter.TryConsume("client-a"),
                "Should be rate-limited after exhausting tokens");

            // Wait long enough for at least one token to refill.
            // At 5 tokens/sec, one token refills every 200ms. Wait 250ms for safety.
            Thread.Sleep(250);

            Assert.IsTrue(limiter.TryConsume("client-a"),
                "Should have refilled at least one token after waiting");
        }

        [Test]
        public void TryConsume_PerClient_IndependentBuckets()
        {
            const int max = 3;
            var limiter = new RateLimiter(max);

            // Exhaust client-a
            for (int i = 0; i < max; i++)
                limiter.TryConsume("client-a");

            Assert.IsFalse(limiter.TryConsume("client-a"),
                "client-a should be rate-limited");

            // client-b should still have tokens
            Assert.IsTrue(limiter.TryConsume("client-b"),
                "client-b should not be affected by client-a's usage");
        }

        [Test]
        public void CleanupStale_RemovesOldBuckets()
        {
            const int max = 5;
            var limiter = new RateLimiter(max);

            // Create a bucket for client-a
            limiter.TryConsume("client-a");

            // CleanupStale should NOT remove a just-used bucket
            limiter.CleanupStale();

            // client-a bucket should still exist (tokens already consumed minus 1)
            // Verify by consuming remaining tokens — if bucket was removed,
            // a fresh bucket with full tokens would be created.
            for (int i = 0; i < max - 1; i++)
                limiter.TryConsume("client-a");

            // If bucket survived cleanup, this should fail (all tokens used).
            // If bucket was incorrectly removed, a fresh bucket would have been
            // created above and this would succeed.
            Assert.IsFalse(limiter.TryConsume("client-a"),
                "Bucket should not have been cleaned up — it was just accessed");
        }

        [Test]
        public void TryConsume_NullClientId_UsesDefault()
        {
            var limiter = new RateLimiter(5);

            Assert.IsTrue(limiter.TryConsume(null),
                "Null client ID should fall back to default bucket");
        }

        [Test]
        public void TryConsume_EmptyClientId_UsesDefault()
        {
            var limiter = new RateLimiter(5);

            Assert.IsTrue(limiter.TryConsume(""),
                "Empty client ID should fall back to default bucket");
        }
    }
}

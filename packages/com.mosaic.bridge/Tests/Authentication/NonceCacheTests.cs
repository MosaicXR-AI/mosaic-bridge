using Mosaic.Bridge.Core.Authentication;
using NUnit.Framework;

namespace Mosaic.Bridge.Tests.Authentication
{
    [TestFixture]
    public class NonceCacheTests
    {
        [Test]
        public void TryConsume_SameNonceTwice_RejectsSecondCall()
        {
            var cache = new NonceCache();
            const long ts = 1_700_000_000;

            Assert.IsTrue(cache.TryConsume("abc", ts));
            Assert.IsFalse(cache.TryConsume("abc", ts));
        }

        [Test]
        public void TryConsume_DifferentNonces_AreAllAccepted()
        {
            var cache = new NonceCache();
            const long ts = 1_700_000_000;

            Assert.IsTrue(cache.TryConsume("a", ts));
            Assert.IsTrue(cache.TryConsume("b", ts));
            Assert.IsTrue(cache.TryConsume("c", ts));
            Assert.AreEqual(3, cache.CurrentCount);
        }

        [Test]
        public void TryConsume_AtCapacity_ReturnsFalse()
        {
            var cache = new NonceCache(bucketCapacity: 2);
            const long ts = 1_700_000_000;

            Assert.IsTrue(cache.TryConsume("a", ts));
            Assert.IsTrue(cache.TryConsume("b", ts));
            // Third nonce in the same bucket — fail closed.
            Assert.IsFalse(cache.TryConsume("c", ts));
        }

        [Test]
        public void TryConsume_NonceFromPreviousBucket_StillRejectedAcrossBoundary()
        {
            var cache = new NonceCache();
            const long bucket1Ts = 1_700_000_000;        // epoch = 28333333
            const long bucket2Ts = bucket1Ts + 60;        // next epoch

            Assert.IsTrue(cache.TryConsume("nonce-x", bucket1Ts));

            // Cross the bucket boundary; the nonce should still be remembered as the
            // current bucket rotates into "previous".
            Assert.IsFalse(cache.TryConsume("nonce-x", bucket2Ts));
        }

        [Test]
        public void TryConsume_NonceForgottenAfterTwoBucketAdvances()
        {
            var cache = new NonceCache();
            const long bucket1Ts = 1_700_000_000;
            const long bucket2Ts = bucket1Ts + 60;
            const long bucket3Ts = bucket1Ts + 120;

            Assert.IsTrue(cache.TryConsume("nonce-y", bucket1Ts));

            // Advance one bucket — must still reject (nonce-y now lives in "previous").
            Assert.IsFalse(cache.TryConsume("nonce-y", bucket2Ts));

            // Advance another full bucket — both buckets should have rotated past it.
            Assert.IsTrue(cache.TryConsume("nonce-y", bucket3Ts));
        }

        [Test]
        public void TryConsume_NullOrEmptyNonce_Rejected()
        {
            var cache = new NonceCache();
            Assert.IsFalse(cache.TryConsume(null, 1));
            Assert.IsFalse(cache.TryConsume(string.Empty, 1));
        }
    }
}

using NUnit.Framework;
using Mosaic.Bridge.Core.Dispatcher;
using Mosaic.Bridge.Core.Server;

namespace Mosaic.Bridge.Tests.Dispatcher
{
    [TestFixture]
    public class BackpressureTests
    {
        [Test]
        public void TryEnqueueClassified_Writes_RejectedAt80Percent()
        {
            var queue = new ToolQueue(capacity: 10);

            // Fill to 80% (8 items) with reads
            for (int i = 0; i < 8; i++)
            {
                var pr = MakePendingRequest("default");
                var result = queue.TryEnqueueClassified(pr, isWrite: false);
                Assert.AreEqual(EnqueueResult.Accepted, result, $"Read {i} should be accepted");
            }

            Assert.AreEqual(8, queue.Count);

            // Write should be rejected at 80% threshold
            var writePr = MakePendingRequest("default");
            var writeResult = queue.TryEnqueueClassified(writePr, isWrite: true);
            Assert.AreEqual(EnqueueResult.RejectedThreshold, writeResult,
                "Write should be rejected when queue is at 80% capacity");

            // Read should still be accepted (under 100%)
            var readPr = MakePendingRequest("default");
            var readResult = queue.TryEnqueueClassified(readPr, isWrite: false);
            Assert.AreEqual(EnqueueResult.Accepted, readResult,
                "Read should still be accepted when queue is at 80%");
        }

        [Test]
        public void TryEnqueueClassified_Reads_AcceptedUntil100Percent()
        {
            var queue = new ToolQueue(capacity: 10);

            // Fill to 100% with reads
            for (int i = 0; i < 10; i++)
            {
                var pr = MakePendingRequest("default");
                var result = queue.TryEnqueueClassified(pr, isWrite: false);
                Assert.AreEqual(EnqueueResult.Accepted, result, $"Read {i} should be accepted");
            }

            Assert.AreEqual(10, queue.Count);

            // 11th read should be rejected (full)
            var overflowPr = MakePendingRequest("default");
            var overflowResult = queue.TryEnqueueClassified(overflowPr, isWrite: false);
            Assert.AreEqual(EnqueueResult.RejectedFull, overflowResult,
                "Read should be rejected when queue is at 100% capacity");
        }

        [Test]
        public void TryEnqueue_BackwardCompatible_WorksAsRead()
        {
            var queue = new ToolQueue(capacity: 10);

            // Fill to 80%
            for (int i = 0; i < 8; i++)
            {
                var pr = MakePendingRequest("default");
                Assert.IsTrue(queue.TryEnqueue(pr), $"Item {i} should be accepted via backward-compatible API");
            }

            // Old TryEnqueue should still work (treated as read, not rejected at 80%)
            var readPr = MakePendingRequest("default");
            Assert.IsTrue(queue.TryEnqueue(readPr),
                "Backward-compatible TryEnqueue should treat as read and accept at 80%");

            // Fill to 100%
            var fillPr = MakePendingRequest("default");
            Assert.IsTrue(queue.TryEnqueue(fillPr), "Should accept 10th item");

            // 11th should be rejected
            var overflowPr = MakePendingRequest("default");
            Assert.IsFalse(queue.TryEnqueue(overflowPr),
                "Backward-compatible TryEnqueue should reject at 100%");
        }

        [Test]
        public void WriteRejectThreshold_Is80PercentOfCapacity()
        {
            var queue = new ToolQueue(capacity: 200);
            Assert.AreEqual(160, queue.WriteRejectThreshold);

            var smallQueue = new ToolQueue(capacity: 10);
            Assert.AreEqual(8, smallQueue.WriteRejectThreshold);
        }

        private static PendingRequest MakePendingRequest(string clientId)
        {
            return new PendingRequest(new HandlerRequest
            {
                Method = "POST",
                RawUrl = "/test",
                Body = new byte[0],
                ClientId = clientId
            })
            {
                ClientId = clientId
            };
        }
    }
}

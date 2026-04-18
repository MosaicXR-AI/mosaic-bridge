using System.Threading.Tasks;
using Mosaic.Bridge.Core.Dispatcher;
using Mosaic.Bridge.Core.Server;
using NUnit.Framework;

namespace Mosaic.Bridge.Tests.Dispatcher
{
    [TestFixture]
    public class ToolQueueTests
    {
        private static PendingRequest MakeRequest() =>
            new PendingRequest(new HandlerRequest { Method = "POST", RawUrl = "/tool", Body = new byte[0] });

        [Test]
        public void TryEnqueue_UpToCapacity_Succeeds()
        {
            var queue = new ToolQueue(capacity: 3);

            Assert.IsTrue(queue.TryEnqueue(MakeRequest()));
            Assert.IsTrue(queue.TryEnqueue(MakeRequest()));
            Assert.IsTrue(queue.TryEnqueue(MakeRequest()));
            Assert.AreEqual(3, queue.Count);
        }

        [Test]
        public void TryEnqueue_BeyondCapacity_ReturnsFalse_RejectOnFull()
        {
            var queue = new ToolQueue(capacity: 2, policy: BackpressurePolicy.RejectOnFull);

            Assert.IsTrue(queue.TryEnqueue(MakeRequest()));
            Assert.IsTrue(queue.TryEnqueue(MakeRequest()));
            Assert.IsFalse(queue.TryEnqueue(MakeRequest()));
            Assert.AreEqual(2, queue.Count);
        }

        [Test]
        public void TryDequeue_FromEmpty_ReturnsFalse()
        {
            var queue = new ToolQueue();

            Assert.IsFalse(queue.TryDequeue(out var result));
            Assert.IsNull(result);
        }

        [Test]
        public void TryEnqueue_ThenDequeue_RoundTrips_SameItem()
        {
            var queue = new ToolQueue();
            var pr = MakeRequest();
            pr.Request.RawUrl = "/specific-url";

            queue.TryEnqueue(pr);
            Assert.IsTrue(queue.TryDequeue(out var dequeued));
            Assert.AreSame(pr, dequeued);
        }

        [Test]
        public void Count_ReflectsEnqueueAndDequeueOperations()
        {
            var queue = new ToolQueue(capacity: 5);

            Assert.AreEqual(0, queue.Count);
            queue.TryEnqueue(MakeRequest());
            queue.TryEnqueue(MakeRequest());
            Assert.AreEqual(2, queue.Count);

            queue.TryDequeue(out _);
            Assert.AreEqual(1, queue.Count);

            queue.TryDequeue(out _);
            Assert.AreEqual(0, queue.Count);
        }

        [Test]
        public void DrainWith_CompletesAllTcs_WithGivenResponse()
        {
            var queue = new ToolQueue(capacity: 5);
            var pr1 = MakeRequest();
            var pr2 = MakeRequest();
            var pr3 = MakeRequest();

            queue.TryEnqueue(pr1);
            queue.TryEnqueue(pr2);
            queue.TryEnqueue(pr3);

            var drainResponse = new HandlerResponse { StatusCode = 503, ContentType = "application/json", Body = "{}" };
            queue.DrainWith(drainResponse);

            Assert.AreEqual(0, queue.Count);

            Assert.IsTrue(pr1.Tcs.Task.IsCompleted);
            Assert.IsTrue(pr2.Tcs.Task.IsCompleted);
            Assert.IsTrue(pr3.Tcs.Task.IsCompleted);

            Assert.AreEqual(503, pr1.Tcs.Task.Result.StatusCode);
            Assert.AreEqual(503, pr2.Tcs.Task.Result.StatusCode);
            Assert.AreEqual(503, pr3.Tcs.Task.Result.StatusCode);

            Assert.AreSame(drainResponse, pr1.Tcs.Task.Result);
            Assert.AreSame(drainResponse, pr2.Tcs.Task.Result);
            Assert.AreSame(drainResponse, pr3.Tcs.Task.Result);
        }
    }
}

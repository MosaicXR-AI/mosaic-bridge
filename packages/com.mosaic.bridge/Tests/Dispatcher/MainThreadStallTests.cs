using System.Text;
using System.Threading;
using NUnit.Framework;
using Mosaic.Bridge.Core.Dispatcher;
using Mosaic.Bridge.Core.Server;

namespace Mosaic.Bridge.Tests.Dispatcher
{
    [TestFixture]
    public class MainThreadStallTests
    {
        private class StubLogger : Mosaic.Bridge.Contracts.Interfaces.IMosaicLogger
        {
            public Mosaic.Bridge.Contracts.Interfaces.LogLevel MinimumLevel { get; set; }
                = Mosaic.Bridge.Contracts.Interfaces.LogLevel.Trace;

            public void Trace(string message, params (string Key, object Value)[] context) { }
            public void Debug(string message, params (string Key, object Value)[] context) { }
            public void Info(string message, params (string Key, object Value)[] context) { }
            public void Warn(string message, params (string Key, object Value)[] context) { }
            public void Error(string message, System.Exception exception = null, params (string Key, object Value)[] context) { }
            public bool IsEnabled(Mosaic.Bridge.Contracts.Interfaces.LogLevel level) => level >= MinimumLevel;
        }

        private StubLogger _logger;

        [SetUp]
        public void SetUp()
        {
            _logger = new StubLogger();
        }

        [Test]
        public void MainThreadStalledMs_ReturnsZero_WhenNotStarted()
        {
            long fakeNow = 10000;
            var queue = new ToolQueue(capacity: 200);
            var dispatcher = new MainThreadDispatcher(
                queue,
                _logger,
                nowProvider: () => fakeNow);

            Assert.AreEqual(0, dispatcher.MainThreadStalledMs,
                "Should return 0 when dispatcher has not been started (lastUpdateTickMs == 0)");
        }

        [Test]
        public void HandleAsync_MainThreadNotStalled_Write_Enqueued()
        {
            long fakeNow = 10000;
            var queue = new ToolQueue(capacity: 200);
            var dispatcher = new MainThreadDispatcher(
                queue,
                _logger,
                requestTimeoutMs: 30000,
                toolReadOnlyLookup: _ => false,
                nowProvider: () => fakeNow);

            var request = MakeWriteRequest();
            var task = dispatcher.HandleAsync(request, CancellationToken.None);

            Assert.IsFalse(task.IsCompleted, "Write should be enqueued when not stalled");
            Assert.AreEqual(1, queue.Count);
        }

        [Test]
        public void HandleAsync_MainThreadStalled_Read_StillEnqueued()
        {
            long fakeNow = 10000;
            var queue = new ToolQueue(capacity: 200);
            var dispatcher = new MainThreadDispatcher(
                queue,
                _logger,
                requestTimeoutMs: 30000,
                toolReadOnlyLookup: _ => true, // all tools are reads
                nowProvider: () => fakeNow);

            var request = MakeReadRequest();
            var task = dispatcher.HandleAsync(request, CancellationToken.None);

            // Read should be enqueued regardless of stall state
            Assert.IsFalse(task.IsCompleted, "Read task should be pending (enqueued)");
            Assert.AreEqual(1, queue.Count);
        }

        [Test]
        public void HandleAsync_QueueFull_Returns503()
        {
            long fakeNow = 10000;
            var queue = new ToolQueue(capacity: 5);
            var dispatcher = new MainThreadDispatcher(
                queue,
                _logger,
                requestTimeoutMs: 30000,
                toolReadOnlyLookup: _ => true,
                nowProvider: () => fakeNow);

            // Fill to capacity
            for (int i = 0; i < 5; i++)
            {
                dispatcher.HandleAsync(MakeReadRequest(), CancellationToken.None);
            }

            var task = dispatcher.HandleAsync(MakeReadRequest(), CancellationToken.None);

            Assert.IsTrue(task.IsCompleted, "Rejected request should complete immediately");
            Assert.AreEqual(503, task.Result.StatusCode, "Should return 503 for backpressure");
        }

        [Test]
        public void HandleAsync_WriteAt80Percent_Returns503()
        {
            long fakeNow = 10000;
            var queue = new ToolQueue(capacity: 10);
            var dispatcher = new MainThreadDispatcher(
                queue,
                _logger,
                requestTimeoutMs: 30000,
                toolReadOnlyLookup: _ => false,
                nowProvider: () => fakeNow);

            // Fill to 80% with reads via direct queue access
            for (int i = 0; i < 8; i++)
            {
                var pr = new PendingRequest(MakeReadRequest()) { ClientId = "default" };
                queue.TryEnqueueClassified(pr, isWrite: false);
            }

            var task = dispatcher.HandleAsync(MakeWriteRequest(), CancellationToken.None);

            Assert.IsTrue(task.IsCompleted, "Rejected write should complete immediately");
            Assert.AreEqual(503, task.Result.StatusCode, "Write should get 503 at 80% threshold");
        }

        [Test]
        public void HandleAsync_ToolNameExtractedFromBody()
        {
            bool lookupCalled = false;
            string lookedUpTool = null;

            var queue = new ToolQueue(capacity: 200);
            var dispatcher = new MainThreadDispatcher(
                queue,
                _logger,
                toolReadOnlyLookup: name =>
                {
                    lookupCalled = true;
                    lookedUpTool = name;
                    return true;
                },
                nowProvider: () => 10000);

            var body = Encoding.UTF8.GetBytes("{\"tool\":\"scene/get_hierarchy\"}");
            var request = new HandlerRequest
            {
                Method = "POST",
                RawUrl = "/tools/call",
                Body = body,
                ClientId = "test-client"
            };

            dispatcher.HandleAsync(request, CancellationToken.None);

            Assert.IsTrue(lookupCalled, "Tool read-only lookup should be called");
            Assert.AreEqual("scene/get_hierarchy", lookedUpTool);
        }

        private static HandlerRequest MakeWriteRequest()
        {
            var body = Encoding.UTF8.GetBytes("{\"tool\":\"gameobject/create\"}");
            return new HandlerRequest
            {
                Method = "POST",
                RawUrl = "/tools/call",
                Body = body,
                ClientId = "default"
            };
        }

        private static HandlerRequest MakeReadRequest()
        {
            var body = Encoding.UTF8.GetBytes("{\"tool\":\"scene/get_hierarchy\"}");
            return new HandlerRequest
            {
                Method = "POST",
                RawUrl = "/tools/call",
                Body = body,
                ClientId = "default"
            };
        }
    }
}

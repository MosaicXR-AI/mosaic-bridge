using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Mosaic.Bridge.Contracts.Interfaces;
using Mosaic.Bridge.Core.Dispatcher;
using Mosaic.Bridge.Core.Server;
using NUnit.Framework;

namespace Mosaic.Bridge.Tests.Dispatcher
{
    [TestFixture]
    public class MainThreadDispatcherTests
    {
        private ToolQueue _queue;
        private RecordingLogger _logger;
        private MainThreadDispatcher _dispatcher;

        [SetUp]
        public void SetUp()
        {
            _queue = new ToolQueue(capacity: 5);
            _logger = new RecordingLogger();
            // Do NOT call Start() — we drive ProcessPendingRequests() manually
            _dispatcher = new MainThreadDispatcher(_queue, _logger);
        }

        private static HandlerRequest MakeRequest() =>
            new HandlerRequest { Method = "POST", RawUrl = "/tool", Body = new byte[0] };

        // ── Tests ───────────────────────────────────────────────────────────────

        [Test]
        public async Task HandleAsync_QueueFull_ReturnsBridgeBusy()
        {
            // Use a separate dispatcher with capacity 2 so we can fill it without awaiting.
            var fullQueue = new ToolQueue(capacity: 2);
            var disp = new MainThreadDispatcher(fullQueue, _logger);

            // Fill it — do NOT await, the tasks are intentionally left pending.
            disp.HandleAsync(MakeRequest(), CancellationToken.None);
            disp.HandleAsync(MakeRequest(), CancellationToken.None);

            // Third enqueue must fail synchronously with 503 because the queue is full.
            var t3 = disp.HandleAsync(MakeRequest(), CancellationToken.None);

            Assert.IsTrue(t3.IsCompleted);
            var response = await t3;
            Assert.AreEqual(503, response.StatusCode);
            StringAssert.Contains("BRIDGE_BACKPRESSURE", response.Body);
        }

        [Test]
        public async Task ProcessPendingRequests_NoRunner_Returns501()
        {
            var task = _dispatcher.HandleAsync(MakeRequest(), CancellationToken.None);

            _dispatcher.ProcessPendingRequests(maxToProcess: 1);

            var response = await task;
            Assert.AreEqual(501, response.StatusCode);
            StringAssert.Contains("not_implemented", response.Body);
        }

        [Test]
        public async Task ProcessPendingRequests_WithRunner_ReturnsRunnerResponse()
        {
            var stubRunner = new StubRunner(new HandlerResponse
            {
                StatusCode = 200,
                ContentType = "application/json",
                Body = "{\"ok\":true}"
            });
            _dispatcher.SetRunner(stubRunner);

            var task = _dispatcher.HandleAsync(MakeRequest(), CancellationToken.None);
            _dispatcher.ProcessPendingRequests(maxToProcess: 1);

            var response = await task;
            Assert.AreEqual(200, response.StatusCode);
            Assert.IsNotNull(stubRunner.LastRequest);
        }

        [Test]
        public async Task ProcessPendingRequests_RunnerThrows_Returns500()
        {
            var throwingRunner = new ThrowingRunner(new InvalidOperationException("boom"));
            _dispatcher.SetRunner(throwingRunner);

            var task = _dispatcher.HandleAsync(MakeRequest(), CancellationToken.None);
            _dispatcher.ProcessPendingRequests(maxToProcess: 1);

            var response = await task;
            Assert.AreEqual(500, response.StatusCode);
            StringAssert.Contains("boom", response.Body);
        }

        [Test]
        public async Task ProcessPendingRequests_TimedOutRequest_Returns504()
        {
            // requestTimeoutMs: 0 means any request is immediately timed out
            var dispatcher = new MainThreadDispatcher(_queue, _logger, requestTimeoutMs: 0);

            var task = dispatcher.HandleAsync(MakeRequest(), CancellationToken.None);

            // Sleep 1ms to ensure EnqueuedUnixMs is in the past relative to the 0ms timeout
            Thread.Sleep(1);

            dispatcher.ProcessPendingRequests(maxToProcess: 1);

            var response = await task;
            Assert.AreEqual(504, response.StatusCode);
            StringAssert.Contains("timeout", response.Body);
        }

        [Test]
        public async Task ProcessPendingRequests_MaxPerTick1_LeavesRemainder()
        {
            var t1 = _dispatcher.HandleAsync(MakeRequest(), CancellationToken.None);
            var t2 = _dispatcher.HandleAsync(MakeRequest(), CancellationToken.None);
            var t3 = _dispatcher.HandleAsync(MakeRequest(), CancellationToken.None);

            Assert.AreEqual(3, _queue.Count);

            _dispatcher.ProcessPendingRequests(maxToProcess: 1);

            Assert.AreEqual(2, _queue.Count);
            Assert.IsTrue(t1.IsCompleted);
            Assert.IsFalse(t2.IsCompleted);
            Assert.IsFalse(t3.IsCompleted);
        }

        [Test]
        public async Task DrainForDomainReload_CompletesAllPending_With503()
        {
            var t1 = _dispatcher.HandleAsync(MakeRequest(), CancellationToken.None);
            var t2 = _dispatcher.HandleAsync(MakeRequest(), CancellationToken.None);
            var t3 = _dispatcher.HandleAsync(MakeRequest(), CancellationToken.None);

            _dispatcher.DrainForDomainReload();

            Assert.IsTrue(t1.IsCompleted);
            Assert.IsTrue(t2.IsCompleted);
            Assert.IsTrue(t3.IsCompleted);

            var r1 = await t1;
            var r2 = await t2;
            var r3 = await t3;

            Assert.AreEqual(503, r1.StatusCode);
            Assert.AreEqual(503, r2.StatusCode);
            Assert.AreEqual(503, r3.StatusCode);

            StringAssert.Contains("DOMAIN_RELOAD", r1.Body);
        }

        // ── Test doubles ────────────────────────────────────────────────────────

        private sealed class StubRunner : IToolRunner
        {
            private readonly HandlerResponse _response;
            public HandlerRequest LastRequest { get; private set; }

            public StubRunner(HandlerResponse response = null)
            {
                _response = response ?? new HandlerResponse { StatusCode = 200, ContentType = "application/json", Body = "{}" };
            }

            public HandlerResponse Execute(HandlerRequest request)
            {
                LastRequest = request;
                return _response;
            }
        }

        private sealed class ThrowingRunner : IToolRunner
        {
            private readonly Exception _exception;

            public ThrowingRunner(Exception exception)
            {
                _exception = exception;
            }

            public HandlerResponse Execute(HandlerRequest request) => throw _exception;
        }

        private sealed class RecordingLogger : IMosaicLogger
        {
            public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;
            public List<string> Warnings { get; } = new List<string>();
            public List<string> Errors { get; } = new List<string>();

            public void Trace(string message, params (string Key, object Value)[] context) { }
            public void Debug(string message, params (string Key, object Value)[] context) { }
            public void Info(string message, params (string Key, object Value)[] context) { }
            public void Warn(string message, params (string Key, object Value)[] context) { Warnings.Add(message); }
            public void Error(string message, Exception exception = null, params (string Key, object Value)[] context) { Errors.Add(message); }
            public bool IsEnabled(LogLevel level) => level >= MinimumLevel;
        }
    }
}

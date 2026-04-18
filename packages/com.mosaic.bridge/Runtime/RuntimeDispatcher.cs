using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Mosaic.Bridge.Runtime
{
    /// <summary>
    /// Pending request queued for main-thread execution at runtime.
    /// </summary>
    internal sealed class RuntimePendingRequest
    {
        public RuntimeHandlerRequest Request { get; }
        public TaskCompletionSource<RuntimeHandlerResponse> Tcs { get; }
        public long EnqueuedUnixMs { get; }
        public bool IsReadOnly { get; set; }
        public string ClientId { get; set; }
        public CancellationToken CancellationToken { get; set; }

        public RuntimePendingRequest(RuntimeHandlerRequest request)
        {
            Request = request;
            Tcs = new TaskCompletionSource<RuntimeHandlerResponse>();
            EnqueuedUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    /// <summary>
    /// Update-based tool dispatch for compiled runtime builds.
    /// Mirrors the editor <c>MainThreadDispatcher</c> pattern: incoming HTTP requests are
    /// queued from the listener thread and processed on the main Unity thread during Update().
    /// Processes up to 1 write or 5 reads per frame.
    /// </summary>
    public sealed class RuntimeDispatcher
    {
        private readonly Queue<RuntimePendingRequest> _queue = new Queue<RuntimePendingRequest>();
        private readonly object _lock = new object();
        private readonly RuntimeLogger _logger;
        private readonly int _requestTimeoutMs;
        private readonly int _maxWritesPerTick;
        private readonly int _maxReadsPerTick;

        private volatile IRuntimeToolRunner _runner;

        /// <summary>Number of requests currently queued.</summary>
        public int QueueCount
        {
            get { lock (_lock) { return _queue.Count; } }
        }

        /// <summary>Maximum queue capacity before rejecting requests.</summary>
        public int Capacity { get; }

        public RuntimeDispatcher(
            RuntimeLogger logger,
            int requestTimeoutMs = 30_000,
            int capacity = 200)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _requestTimeoutMs = requestTimeoutMs;
            Capacity = capacity;
            _maxWritesPerTick = 1;
            _maxReadsPerTick = 5;
        }

        /// <summary>
        /// Sets the tool runner that processes requests on the main thread.
        /// </summary>
        public void SetRunner(IRuntimeToolRunner runner)
        {
            _runner = runner;
        }

        /// <summary>
        /// Enqueues a request from the HTTP listener thread.
        /// Returns a Task that completes when the request is processed on the main thread.
        /// Returns null if the queue is full (backpressure).
        /// </summary>
        public Task<RuntimeHandlerResponse> EnqueueAsync(RuntimeHandlerRequest request, CancellationToken ct)
        {
            var pr = new RuntimePendingRequest(request)
            {
                ClientId = request.ClientId ?? "default"
            };

            // Classify read vs write based on HTTP method
            pr.IsReadOnly = string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase);

            lock (_lock)
            {
                if (_queue.Count >= Capacity)
                    return null; // caller should return 503

                _queue.Enqueue(pr);
            }

            pr.CancellationToken = ct;
            ct.Register(() => pr.Tcs.TrySetCanceled());
            return pr.Tcs.Task;
        }

        /// <summary>
        /// Called from MonoBehaviour.Update() on the main thread.
        /// Processes queued requests within the per-frame budget.
        /// </summary>
        public void ProcessPendingRequests(int maxToProcess = -1)
        {
            int writesProcessed = 0;
            int readsProcessed = 0;
            int maxWrites = maxToProcess >= 0 ? maxToProcess : _maxWritesPerTick;
            int maxReads = maxToProcess >= 0 ? maxToProcess : _maxReadsPerTick;
            int maxTotal = maxToProcess >= 0 ? maxToProcess : (maxWrites + maxReads);

            for (int i = 0; i < maxTotal; i++)
            {
                RuntimePendingRequest pr;
                lock (_lock)
                {
                    if (_queue.Count == 0)
                        break;
                    pr = _queue.Dequeue();
                }

                if (pr.IsReadOnly)
                    readsProcessed++;
                else
                    writesProcessed++;

                var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                if ((nowMs - pr.EnqueuedUnixMs) > _requestTimeoutMs)
                {
                    pr.Tcs.TrySetResult(TimeoutResponse());
                    continue;
                }

                var runner = _runner;
                RuntimeHandlerResponse response;

                try
                {
                    response = runner != null
                        ? runner.Execute(pr.Request)
                        : NotImplementedResponse();
                }
                catch (OperationCanceledException)
                {
                    _logger.Info("Tool execution cancelled");
                    response = CancelledResponse();
                }
                catch (Exception ex)
                {
                    _logger.Error($"Unhandled exception in tool '{pr.Request.RawUrl}': {ex.Message}", ex);
                    var sanitizedMessage = $"{ex.GetType().Name}: {ex.Message}";
                    response = new RuntimeHandlerResponse
                    {
                        StatusCode = 500,
                        ContentType = "application/json",
                        Body = $"{{\"success\":false,\"error\":\"{EscapeJson(sanitizedMessage)}\",\"errorCode\":\"TOOL_EXECUTION_FAILED\",\"suggestedFix\":\"The tool encountered an unexpected error. Check Unity console for details.\"}}"
                    };
                }

                pr.Tcs.TrySetResult(response);
            }

            int total = writesProcessed + readsProcessed;
            if (total > 0)
                _logger.Trace($"Processed {total} requests this tick (w={writesProcessed}, r={readsProcessed})");
        }

        /// <summary>
        /// Drains all queued requests, completing each with a 503 response.
        /// Called during shutdown.
        /// </summary>
        public int Drain()
        {
            int count = 0;
            var response = new RuntimeHandlerResponse
            {
                StatusCode = 503,
                ContentType = "application/json",
                Body = "{\"error\":\"DOMAIN_RELOAD\"}"
            };

            lock (_lock)
            {
                while (_queue.Count > 0)
                {
                    var pr = _queue.Dequeue();
                    pr.Tcs.TrySetResult(response);
                    count++;
                }
            }

            if (count > 0)
                _logger.Warn($"Queue drained on shutdown: {count} requests");

            return count;
        }

        private static RuntimeHandlerResponse TimeoutResponse() => new RuntimeHandlerResponse
        {
            StatusCode = 504,
            ContentType = "application/json",
            Body = "{\"error\":\"timeout\"}"
        };

        private static RuntimeHandlerResponse NotImplementedResponse() => new RuntimeHandlerResponse
        {
            StatusCode = 501,
            ContentType = "application/json",
            Body = "{\"error\":\"not_implemented\"}"
        };

        private static RuntimeHandlerResponse CancelledResponse() => new RuntimeHandlerResponse
        {
            StatusCode = 499,
            ContentType = "application/json",
            Body = "{\"error\":\"CANCELLED\",\"message\":\"Tool execution was cancelled by the client\"}"
        };

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }

    /// <summary>
    /// Interface for executing tool requests on the main thread at runtime.
    /// </summary>
    public interface IRuntimeToolRunner
    {
        RuntimeHandlerResponse Execute(RuntimeHandlerRequest request);
    }
}

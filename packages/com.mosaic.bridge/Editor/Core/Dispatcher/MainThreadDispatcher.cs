using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Interfaces;
using Mosaic.Bridge.Core.Server;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Mosaic.Bridge.Core.Dispatcher
{
    public sealed class MainThreadDispatcher : IRequestHandler
    {
        private readonly ToolQueue _queue;
        private readonly IMosaicLogger _logger;
        private readonly int _requestTimeoutMs;
        private readonly int _maxWritesPerTick;
        private readonly int _maxReadsPerTick;
        private readonly Func<string, bool> _toolReadOnlyLookup;
        private readonly Func<long> _nowProvider;

        private volatile IToolRunner _runner;
        private long _lastUpdateTickMs;

        public long MainThreadStalledMs
        {
            get
            {
                var last = Interlocked.Read(ref _lastUpdateTickMs);
                if (last == 0) return 0; // not started yet
                return _nowProvider() - last;
            }
        }

        public MainThreadDispatcher(
            ToolQueue queue,
            IMosaicLogger logger,
            int requestTimeoutMs = 30_000,
            Func<string, bool> toolReadOnlyLookup = null,
            Func<long> nowProvider = null)
        {
            _queue = queue;
            _logger = logger;
            _requestTimeoutMs = requestTimeoutMs;
            _maxWritesPerTick = 1;
            _maxReadsPerTick = 5;
            _toolReadOnlyLookup = toolReadOnlyLookup ?? (_ => false); // default: unknown tools are writes
            _nowProvider = nowProvider ?? (() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            _lastUpdateTickMs = 0;
        }

        // IRequestHandler -- called on ThreadPool
        public Task<HandlerResponse> HandleAsync(HandlerRequest request, CancellationToken ct)
        {
            // Extract tool name and determine read/write classification
            bool isReadOnly = false;
            try
            {
                if (request.Body != null && request.Body.Length > 0)
                {
                    var bodyStr = Encoding.UTF8.GetString(request.Body);
                    var json = JObject.Parse(bodyStr);
                    var toolName = json["tool"]?.Value<string>();
                    if (toolName != null)
                        isReadOnly = _toolReadOnlyLookup(toolName);
                }
            }
            catch { /* parse failure - treat as write (fail closed) */ }

            bool isWrite = !isReadOnly;
            var pr = new PendingRequest(request)
            {
                IsReadOnly = isReadOnly,
                ClientId = request.ClientId ?? "default"
            };

            // Main-thread stall detection for writes (AC4)
            if (isWrite)
            {
                long stalledMs = MainThreadStalledMs;
                long estimatedWaitMs = (_queue.Count * 50L) + stalledMs;
                if (estimatedWaitMs > 5000 && stalledMs > 1000)
                {
                    return Task.FromResult(Accepted202Response());
                }
            }

            var result = _queue.TryEnqueueClassified(pr, isWrite);
            if (result != EnqueueResult.Accepted)
                return Task.FromResult(BackpressureResponse());

            pr.CancellationToken = ct;
            ct.Register(() => pr.Tcs.TrySetCanceled());
            return pr.Tcs.Task;
        }

        // Called from EditorApplication.update (main thread) or directly from tests.
        public void ProcessPendingRequests(int maxToProcess = -1)
        {
            int writesProcessed = 0;
            int readsProcessed = 0;
            int maxWrites = maxToProcess >= 0 ? maxToProcess : _maxWritesPerTick;
            int maxReads = maxToProcess >= 0 ? maxToProcess : _maxReadsPerTick;
            int maxTotal = maxToProcess >= 0 ? maxToProcess : (maxWrites + maxReads);

            for (int i = 0; i < maxTotal; i++)
            {
                if (!_queue.TryDequeue(out var pr))
                    break;

                // Track budget (already dequeued, must process)
                if (pr.IsReadOnly)
                    readsProcessed++;
                else
                    writesProcessed++;

                if ((_nowProvider() - pr.EnqueuedUnixMs) > _requestTimeoutMs)
                {
                    pr.Tcs.TrySetResult(TimeoutResponse());
                    continue;
                }

                var runner = _runner; // volatile read
                HandlerResponse response;

                // Story 1.7: Wrap tool execution in Undo group for partial-state rollback
                string undoGroupName = $"Mosaic: {pr.Request.RawUrl}";
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName(undoGroupName);

                // Story 2.11: Set ambient CancellationToken so tools can opt-in to cancellation
                ToolExecutionContext.Set(pr.CancellationToken);
                try
                {
                    response = runner != null
                        ? runner.Execute(pr.Request)
                        : NotImplementedResponse();
                }
                catch (OperationCanceledException)
                {
                    // Story 2.11: Tool honoured cancellation — return cancelled response
                    _logger.Info("Tool execution cancelled",
                        ("toolRoute", (object)pr.Request.RawUrl));
                    response = CancelledResponse();
                }
                catch (Exception ex)
                {
                    // FR17: Attempt partial-state rollback via Undo system
                    try { Undo.RevertAllInCurrentGroup(); }
                    catch (Exception undoEx)
                    {
                        _logger.Warn($"Undo rollback failed after tool exception: {undoEx.Message}");
                    }

                    // Log full exception for diagnostics (FR59: stack trace in logs only)
                    _logger.Error(
                        $"Unhandled exception in tool '{pr.Request.RawUrl}': {ex.Message}",
                        ex,
                        ("toolRoute", pr.Request.RawUrl)
                    );

                    // Return sanitized error: type + message, NO stack trace (NFR85)
                    var sanitizedMessage = $"{ex.GetType().Name}: {ex.Message}";
                    response = new HandlerResponse
                    {
                        StatusCode = 500,
                        ContentType = "application/json",
                        Body = $"{{\"success\":false,\"error\":\"{EscapeJson(sanitizedMessage)}\",\"errorCode\":\"{ErrorCodes.TOOL_EXECUTION_FAILED}\",\"suggestedFix\":\"The tool encountered an unexpected error. Check Unity console for details.\"}}"
                    };
                }
                finally
                {
                    ToolExecutionContext.Clear();
                }

                pr.Tcs.TrySetResult(response);
            }

            int total = writesProcessed + readsProcessed;
            if (total > 0)
                _logger.Trace($"Processed {total} requests this tick",
                    ("writes", (object)writesProcessed),
                    ("reads", (object)readsProcessed));
        }

        public void SetRunner(IToolRunner runner)
        {
            _runner = runner; // volatile write
        }

        public void DrainForDomainReload()
        {
            var response = new HandlerResponse
            {
                StatusCode = 503,
                ContentType = "application/json",
                Body = $"{{\"error\":\"{ErrorCodes.DOMAIN_RELOAD}\"}}"
            };
            int drained = _queue.DrainWith(response);
            if (drained > 0)
                _logger.Warn("Queue drained for domain reload", ("count", (object)drained));
        }

        public void Start()
        {
            Interlocked.Exchange(ref _lastUpdateTickMs, _nowProvider());
            EditorApplication.update += OnUpdate;
            _logger.Info("MainThreadDispatcher started");
        }

        public void Stop()
        {
            EditorApplication.update -= OnUpdate;
            DrainForDomainReload();
            _logger.Info("MainThreadDispatcher stopped");
        }

        private void OnUpdate()
        {
            Interlocked.Exchange(ref _lastUpdateTickMs, _nowProvider());
            ProcessPendingRequests();
        }

        private static HandlerResponse BackpressureResponse() => new HandlerResponse
        {
            StatusCode = 503,
            ContentType = "application/json",
            Body = $"{{\"error\":\"{ErrorCodes.BRIDGE_BACKPRESSURE}\",\"retryAfter\":5}}",
            Headers = new System.Collections.Generic.Dictionary<string, string> { { "Retry-After", "5" } }
        };

        private static HandlerResponse Accepted202Response() => new HandlerResponse
        {
            StatusCode = 202,
            ContentType = "application/json",
            Body = $"{{\"status\":\"accepted\",\"retryAfter\":2,\"reason\":\"{ErrorCodes.MAIN_THREAD_BLOCKED}\"}}",
            Headers = new System.Collections.Generic.Dictionary<string, string> { { "Retry-After", "2" } }
        };

        private static HandlerResponse TimeoutResponse() => new HandlerResponse
        {
            StatusCode = 504,
            ContentType = "application/json",
            Body = "{\"error\":\"timeout\"}"
        };

        private static HandlerResponse NotImplementedResponse() => new HandlerResponse
        {
            StatusCode = 501,
            ContentType = "application/json",
            Body = "{\"error\":\"not_implemented\"}"
        };

        private static HandlerResponse CancelledResponse() => new HandlerResponse
        {
            StatusCode = 499,
            ContentType = "application/json",
            Body = $"{{\"error\":\"{ErrorCodes.CANCELLED}\",\"message\":\"Tool execution was cancelled by the client\"}}"
        };

        private static string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }
    }
}

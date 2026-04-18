using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Mosaic.Bridge.Contracts.Interfaces;
using Mosaic.Bridge.Core.Diagnostics;
using Mosaic.Bridge.Core.Discovery;
using Mosaic.Bridge.Core.Dispatcher;
using Mosaic.Bridge.Core.Runtime;
using Mosaic.Bridge.Core.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Mosaic.Bridge.Core.Pipeline
{
    /// <summary>
    /// Decorator around ToolRegistry that adds configurable pre/post execution stages.
    /// In Direct mode, passes through to the inner runner with zero overhead.
    /// In higher modes, runs semantic validation, KB advisor, visual verification, etc.
    /// </summary>
    public sealed class ExecutionPipeline : IToolRunner
    {
        private readonly IToolRunner _inner;
        private readonly PipelineConfiguration _config;
        private readonly IMosaicLogger _logger;
        private readonly Func<string, ToolRegistryEntry> _toolLookup;

        private readonly List<IPipelineStage> _preStages = new List<IPipelineStage>();
        private readonly List<IPipelineStage> _postStages = new List<IPipelineStage>();

        public ExecutionPipeline(
            IToolRunner inner,
            PipelineConfiguration config,
            IMosaicLogger logger,
            Func<string, ToolRegistryEntry> toolLookup)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _toolLookup = toolLookup ?? throw new ArgumentNullException(nameof(toolLookup));
        }

        /// <summary>Registers a stage that runs BEFORE tool execution.</summary>
        public void AddPreStage(IPipelineStage stage) => _preStages.Add(stage);

        /// <summary>Registers a stage that runs AFTER tool execution.</summary>
        public void AddPostStage(IPipelineStage stage) => _postStages.Add(stage);

        public HandlerResponse Execute(HandlerRequest request)
        {
            // Parse execution context from request body
            var context = BuildContext(request);

            // Direct mode: zero overhead passthrough (still log diagnostics)
            if (context.Mode == ExecutionMode.Direct)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var directResult = _inner.Execute(request);
                sw.Stop();
                ToolCallLogger.Record(context.ToolName ?? "unknown", directResult.StatusCode, sw.Elapsed.TotalMilliseconds);

                // Story 10.2: Record telemetry (opt-in, local-only)
                UsageTelemetry.RecordToolCall(
                    context.ToolName ?? "unknown",
                    context.Mode.ToString().ToLowerInvariant(),
                    sw.Elapsed.TotalMilliseconds);

                return directResult;
            }

            // Run pre-execution stages (validation, KB advisor)
            HandlerResponse preResult = null;
            foreach (var stage in _preStages)
            {
                // Story 2.11: Check cancellation between pipeline stages
                ToolExecutionContext.CancellationToken.ThrowIfCancellationRequested();

                if (!ShouldRunStage(stage, context))
                    continue;

                if (!stage.Execute(context, ref preResult))
                {
                    // Stage rejected the request — return its response
                    _logger.Info("Pipeline pre-stage rejected request",
                        ("stage", (object)stage.GetType().Name),
                        ("tool", (object)context.ToolName));
                    return preResult ?? ErrorResponse("Pipeline stage rejected the request");
                }
            }

            // Execute the actual tool (with timing for diagnostics)
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var toolResult = _inner.Execute(request);
            stopwatch.Stop();

            // Log to diagnostics ring buffer
            ToolCallLogger.Record(
                context.ToolName ?? "unknown",
                toolResult.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds);

            // Story 10.2: Record telemetry (opt-in, local-only)
            UsageTelemetry.RecordToolCall(
                context.ToolName ?? "unknown",
                context.Mode.ToString().ToLowerInvariant(),
                stopwatch.Elapsed.TotalMilliseconds);

            // Run post-execution stages (screenshots, code review, AI review context)
            foreach (var stage in _postStages)
            {
                // Story 2.11: Check cancellation between pipeline stages
                ToolExecutionContext.CancellationToken.ThrowIfCancellationRequested();

                if (!ShouldRunStage(stage, context))
                    continue;

                stage.Execute(context, ref toolResult);
            }

            // Merge pipeline context into the tool result
            return MergeContext(toolResult, context);
        }

        private ExecutionContext BuildContext(HandlerRequest request)
        {
            var context = new ExecutionContext
            {
                Request = request,
                Mode = _config.DefaultMode
            };

            // Parse tool name, parameters, and execution_mode from request body
            try
            {
                if (request.Body != null && request.Body.Length > 0)
                {
                    var bodyStr = Encoding.UTF8.GetString(request.Body);
                    var json = JObject.Parse(bodyStr);

                    context.ToolName = json["tool"]?.Value<string>();
                    context.Parameters = json["parameters"] as JObject;

                    // Per-call execution_mode override
                    var modeStr = json["execution_mode"]?.Value<string>();
                    if (!string.IsNullOrEmpty(modeStr))
                    {
                        var parsed = PipelineConfiguration.ParseMode(modeStr);
                        if (parsed == ExecutionMode.Direct && modeStr != "direct")
                        {
                            context.Warnings.Add($"Unknown execution_mode '{modeStr}', falling back to direct.");
                        }
                        context.Mode = parsed;
                    }
                }
            }
            catch
            {
                // Parse failure — use defaults, tool registry will handle the error
            }

            // Look up tool metadata
            if (!string.IsNullOrEmpty(context.ToolName))
            {
                context.ToolEntry = _toolLookup(context.ToolName);
            }

            return context;
        }

        private bool ShouldRunStage(IPipelineStage stage, ExecutionContext context)
        {
            // Future: stages can declare their minimum mode requirement via attribute or interface
            // For now, all registered stages run when mode >= Validated
            return context.Mode >= ExecutionMode.Validated;
        }

        private HandlerResponse MergeContext(HandlerResponse toolResult, ExecutionContext context)
        {
            // Parse the existing response body and merge pipeline data
            try
            {
                var body = JObject.Parse(toolResult.Body);

                if (context.Warnings.Count > 0)
                {
                    var existing = body["warnings"] as JArray ?? new JArray();
                    foreach (var w in context.Warnings)
                        existing.Add(w);
                    body["warnings"] = existing;
                }

                if (context.KBReferences.Count > 0)
                {
                    var existing = body["knowledgeBaseReferences"] as JArray ?? new JArray();
                    foreach (var r in context.KBReferences)
                        existing.Add(r);
                    body["knowledgeBaseReferences"] = existing;
                }

                if (context.Screenshots.Count > 0)
                {
                    var arr = new JArray();
                    foreach (var s in context.Screenshots)
                    {
                        arr.Add(new JObject
                        {
                            ["angleLabel"] = s.AngleLabel,
                            ["base64Png"] = s.Base64Png,
                            ["width"] = s.Width,
                            ["height"] = s.Height
                        });
                    }
                    body["screenshots"] = arr;
                }

                if (context.ReviewSummary != null)
                {
                    body["reviewContext"] = context.ReviewSummary;
                }

                body["executionMode"] = context.Mode.ToString().ToLowerInvariant();

                return new HandlerResponse
                {
                    StatusCode = toolResult.StatusCode,
                    ContentType = toolResult.ContentType,
                    Body = body.ToString(Formatting.None),
                    Headers = toolResult.Headers
                };
            }
            catch
            {
                // If body isn't valid JSON, return as-is
                return toolResult;
            }
        }

        private static HandlerResponse ErrorResponse(string message)
        {
            return new HandlerResponse
            {
                StatusCode = 400,
                ContentType = "application/json",
                Body = JsonConvert.SerializeObject(new { error = "PIPELINE_REJECTED", message })
            };
        }
    }
}

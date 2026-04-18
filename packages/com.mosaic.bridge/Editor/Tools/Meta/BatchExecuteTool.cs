using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Core.Bootstrap;
using Mosaic.Bridge.Core.Discovery;
using Newtonsoft.Json;

namespace Mosaic.Bridge.Tools.Meta
{
    public static class BatchExecuteTool
    {
        [MosaicTool("meta/batch_execute",
                    "Executes multiple tool calls sequentially in a single Undo group. " +
                    "Set StopOnError=true to abort remaining calls on first failure.",
                    isReadOnly: false)]
        public static ToolResult<BatchExecuteResult> Execute(BatchExecuteParams p)
        {
            if (p.Calls == null || p.Calls.Count == 0)
                return ToolResult<BatchExecuteResult>.Fail(
                    "Calls array is required and must not be empty", ErrorCodes.INVALID_PARAM);

            var registry = BridgeBootstrap.ToolRegistry;
            if (registry == null)
                return ToolResult<BatchExecuteResult>.Fail(
                    "ToolRegistry not initialized", ErrorCodes.INTERNAL_ERROR);

            var totalSw = Stopwatch.StartNew();
            var results = new List<BatchCallResult>(p.Calls.Count);
            int successCount = 0, failCount = 0, skippedCount = 0;
            bool stopped = false;

            Undo.SetCurrentGroupName("Mosaic: Batch Execute");
            int undoGroup = Undo.GetCurrentGroup();

            foreach (var call in p.Calls)
            {
                if (stopped)
                {
                    results.Add(new BatchCallResult
                    {
                        ToolName = call.ToolName ?? "(null)",
                        Skipped = true
                    });
                    skippedCount++;
                    continue;
                }

                var callSw = Stopwatch.StartNew();

                if (string.IsNullOrEmpty(call.ToolName))
                {
                    callSw.Stop();
                    results.Add(new BatchCallResult
                    {
                        ToolName = "(null)",
                        Success = false,
                        Error = "ToolName is required",
                        DurationMs = callSw.ElapsedMilliseconds
                    });
                    failCount++;
                    if (p.StopOnError) stopped = true;
                    continue;
                }

                var entry = registry.GetEntry(call.ToolName);
                if (entry == null)
                {
                    callSw.Stop();
                    results.Add(new BatchCallResult
                    {
                        ToolName = call.ToolName,
                        Success = false,
                        Error = $"Tool not found: '{call.ToolName}'",
                        DurationMs = callSw.ElapsedMilliseconds
                    });
                    failCount++;
                    if (p.StopOnError) stopped = true;
                    continue;
                }

                try
                {
                    object paramObj = null;
                    if (entry.ParamType != null)
                    {
                        var json = call.Arguments?.ToString(Formatting.None) ?? "{}";
                        var validation = ParameterValidator.Bind(json, entry.ParamType);
                        if (!validation.IsValid)
                        {
                            callSw.Stop();
                            results.Add(new BatchCallResult
                            {
                                ToolName = call.ToolName,
                                Success = false,
                                Error = validation.ErrorMessage,
                                DurationMs = callSw.ElapsedMilliseconds
                            });
                            failCount++;
                            if (p.StopOnError) stopped = true;
                            continue;
                        }
                        paramObj = validation.Value;
                    }

                    var args = entry.ParamType != null
                        ? new[] { paramObj }
                        : Array.Empty<object>();

                    var result = entry.Method.Invoke(null, args);
                    callSw.Stop();

                    results.Add(new BatchCallResult
                    {
                        ToolName = call.ToolName,
                        Success = true,
                        Data = result,
                        DurationMs = callSw.ElapsedMilliseconds
                    });
                    successCount++;
                }
                catch (TargetInvocationException tie)
                {
                    callSw.Stop();
                    var inner = tie.InnerException ?? tie;
                    results.Add(new BatchCallResult
                    {
                        ToolName = call.ToolName,
                        Success = false,
                        Error = inner.Message,
                        DurationMs = callSw.ElapsedMilliseconds
                    });
                    failCount++;
                    if (p.StopOnError) stopped = true;
                }
                catch (Exception ex)
                {
                    callSw.Stop();
                    results.Add(new BatchCallResult
                    {
                        ToolName = call.ToolName,
                        Success = false,
                        Error = ex.Message,
                        DurationMs = callSw.ElapsedMilliseconds
                    });
                    failCount++;
                    if (p.StopOnError) stopped = true;
                }
            }

            Undo.CollapseUndoOperations(undoGroup);
            totalSw.Stop();

            return ToolResult<BatchExecuteResult>.Ok(new BatchExecuteResult
            {
                TotalCalls = p.Calls.Count,
                SuccessCount = successCount,
                FailCount = failCount,
                SkippedCount = skippedCount,
                TotalDurationMs = totalSw.ElapsedMilliseconds,
                Results = results
            });
        }
    }
}

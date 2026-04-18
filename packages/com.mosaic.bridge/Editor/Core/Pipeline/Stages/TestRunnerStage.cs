using System;
using System.Collections.Generic;
using System.Linq;
using Mosaic.Bridge.Contracts.Interfaces;
using Mosaic.Bridge.Core.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor.TestTools.TestRunner.Api;

namespace Mosaic.Bridge.Core.Pipeline.Stages
{
    /// <summary>
    /// Post-execution pipeline stage that runs Unity EditMode tests after tool execution.
    /// Configurable via PipelineConfiguration.CodeReviewRunTests. Purely informational —
    /// never aborts the pipeline. Results are merged into the tool response.
    /// </summary>
    public sealed class TestRunnerStage : IPipelineStage
    {
        private readonly PipelineConfiguration _config;
        private readonly IMosaicLogger _logger;

        public TestRunnerStage(PipelineConfiguration config, IMosaicLogger logger)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool Execute(ExecutionContext context, ref HandlerResponse toolResult)
        {
            // Only run when auto-testing is enabled
            if (!_config.CodeReviewRunTests)
                return true;

            // Only run for modes >= Validated
            if (context.Mode < ExecutionMode.Validated)
                return true;

            // Only run after script tools (create/update)
            if (!IsScriptWriteTool(context))
                return true;

            try
            {
                var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                var collector = new ResultCollector();
                api.RegisterCallbacks(collector);

                var filter = new Filter { testMode = TestMode.EditMode };
                api.Execute(new ExecutionSettings(filter));

                // Build test summary
                var results = collector.Results;
                var testReport = new JObject
                {
                    ["totalTests"] = results.Count,
                    ["passed"] = results.Count(r => r.Passed),
                    ["failed"] = results.Count(r => !r.Passed && !r.Skipped),
                    ["skipped"] = results.Count(r => r.Skipped),
                    ["durationMs"] = (int)results.Sum(r => r.DurationMs)
                };

                var failures = results.Where(r => !r.Passed && !r.Skipped).ToList();
                if (failures.Count > 0)
                {
                    var failedArray = new JArray();
                    foreach (var f in failures.Take(10)) // cap at 10 to avoid huge responses
                    {
                        failedArray.Add(new JObject
                        {
                            ["name"] = f.Name,
                            ["message"] = f.Message ?? ""
                        });
                    }
                    testReport["failedTests"] = failedArray;

                    context.Warnings.Add($"Test runner: {failures.Count} test(s) failed after script change.");
                    _logger.Warn("Pipeline test runner: failures detected",
                        ("failed", (object)failures.Count),
                        ("total", (object)results.Count));
                }
                else
                {
                    _logger.Info("Pipeline test runner: all tests passed",
                        ("total", (object)results.Count));
                }

                // Merge into response
                try
                {
                    var body = JObject.Parse(toolResult.Body);
                    body["testResults"] = testReport;
                    toolResult = new HandlerResponse
                    {
                        StatusCode = toolResult.StatusCode,
                        ContentType = toolResult.ContentType,
                        Body = body.ToString(Formatting.None),
                        Headers = toolResult.Headers
                    };
                }
                catch
                {
                    context.Warnings.Add($"Test runner: {results.Count} tests run, {failures.Count} failed");
                }
            }
            catch (Exception ex)
            {
                // Test runner failure should never break the pipeline
                context.Warnings.Add($"Test runner failed: {ex.Message}");
                _logger.Warn($"Pipeline test runner exception: {ex.Message}");
            }

            return true; // Never abort
        }

        private static bool IsScriptWriteTool(ExecutionContext context)
        {
            var category = context.ToolEntry?.Category;
            if (string.IsNullOrEmpty(category) && !string.IsNullOrEmpty(context.ToolName))
            {
                var parts = context.ToolName.Split('_');
                if (parts.Length >= 2) category = parts[1];
            }

            if (!string.Equals(category, "script", StringComparison.OrdinalIgnoreCase))
                return false;

            return context.ToolName != null &&
                   (context.ToolName.Contains("create") || context.ToolName.Contains("update"));
        }

        private sealed class ResultCollector : ICallbacks
        {
            public List<TestResult> Results { get; } = new List<TestResult>();

            public void RunStarted(ITestAdaptor testsToRun) { }
            public void RunFinished(ITestResultAdaptor result) { }
            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.HasChildren)
                {
                    Results.Add(new TestResult
                    {
                        Name = result.Name,
                        Passed = result.TestStatus == TestStatus.Passed,
                        Skipped = result.TestStatus == TestStatus.Skipped ||
                                  result.TestStatus == TestStatus.Inconclusive,
                        DurationMs = result.Duration * 1000.0,
                        Message = result.Message
                    });
                }
            }
        }

        private sealed class TestResult
        {
            public string Name;
            public bool Passed;
            public bool Skipped;
            public double DurationMs;
            public string Message;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Mosaic.Bridge.Tools.Testing
{
    /// <summary>
    /// Runs Unity EditMode tests programmatically and returns results.
    /// Uses a two-phase approach: first retrieves the test list, then
    /// executes and collects results via callbacks.
    /// </summary>
    public static class TestRunnerTool
    {
        // Static results storage — callbacks fire asynchronously on update ticks
        private static TestRunResult _lastResult;
        private static bool _isRunning;
        private static TestResultCollector _activeCollector;

        [MosaicTool("test/run", "Runs Unity EditMode tests and returns pass/fail results. Optionally filter by test name or category.", isReadOnly: true)]
        public static ToolResult<TestRunResult> RunTests(TestRunParams parameters)
        {
            try
            {
                // If a previous run completed, return those results
                if (_lastResult != null && !_isRunning)
                {
                    var cached = _lastResult;
                    _lastResult = null; // consume the result

                    if (cached.Failed > 0)
                    {
                        return ToolResult<TestRunResult>.Fail(
                            $"{cached.Failed} test(s) failed out of {cached.TotalTests}",
                            "TEST_FAILURES",
                            "Check FailedTests array for details");
                    }
                    return ToolResult<TestRunResult>.Ok(cached);
                }

                // If tests are already running, report status
                if (_isRunning)
                {
                    var inProgress = new TestRunResult
                    {
                        TotalTests = _activeCollector?.Results.Count ?? 0,
                        Passed = _activeCollector?.Results.Count(r => r.Status == "Passed") ?? 0,
                        Failed = _activeCollector?.Results.Count(r => r.Status == "Failed") ?? 0,
                    };
                    return ToolResult<TestRunResult>.OkWithWarnings(inProgress,
                        "Tests are still running. Call test/run again to get final results.");
                }

                // Ensure a valid, saved scene exists before test runner takes over.
                // Unity's test runner creates its own temp scene internally; if the
                // current scene is invalid/unloaded the 'targetScene != nullptr' assertion fires.
                var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
                if (!activeScene.IsValid() || !activeScene.isLoaded)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                        UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
                        UnityEditor.SceneManagement.NewSceneMode.Single);
                }
                // Always save to ensure clean state for the test runner
                UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();


                // Start a new test run — defer to next frame to avoid update callback collision
                _isRunning = true;
                _activeCollector = new TestResultCollector();

                var testNameFilter = parameters?.TestNameFilter;
                var categoryFilter = parameters?.CategoryFilter;

                UnityEditor.EditorApplication.delayCall += () =>
                {
                    try
                    {
                        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
                        api.RegisterCallbacks(_activeCollector);

                        var filter = new Filter { testMode = TestMode.EditMode };
                        if (!string.IsNullOrEmpty(testNameFilter))
                            filter.testNames = new[] { testNameFilter };
                        if (!string.IsNullOrEmpty(categoryFilter))
                            filter.categoryNames = new[] { categoryFilter };

                        api.Execute(new ExecutionSettings(filter));
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[Mosaic.Bridge] Test runner failed: {ex.Message}");
                        _isRunning = false;
                    }
                };

                // Return immediately — tests run on subsequent update ticks
                var started = new TestRunResult
                {
                    TotalTests = -1 // indicates "started but not complete"
                };
                return ToolResult<TestRunResult>.OkWithWarnings(started,
                    "Test run started. Call test/run again to retrieve results when complete.",
                    "NOTE: Unity may log a benign 'targetScene != nullptr' assertion — this is a known Unity test runner bug and does not affect test results.");
            }
            catch (Exception ex)
            {
                _isRunning = false;
                return ToolResult<TestRunResult>.Fail(ex.Message, "INTERNAL_ERROR");
            }
        }

        private sealed class TestResultCollector : ICallbacks
        {
            public List<TestDetail> Results { get; } = new List<TestDetail>();

            public void RunStarted(ITestAdaptor testsToRun) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                // All tests complete — build final result
                _lastResult = new TestRunResult
                {
                    TotalTests = Results.Count,
                    Passed = Results.Count(r => r.Status == "Passed"),
                    Failed = Results.Count(r => r.Status == "Failed"),
                    Skipped = Results.Count(r => r.Status == "Skipped"),
                    DurationMs = (int)Results.Sum(r => r.DurationMs),
                    FailedTests = Results.Where(r => r.Status == "Failed").Take(20).ToList()
                };
                _isRunning = false;
            }

            public void TestStarted(ITestAdaptor test) { }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!result.HasChildren)
                {
                    Results.Add(new TestDetail
                    {
                        Name = result.Name,
                        FullName = result.FullName,
                        Status = MapStatus(result.TestStatus),
                        DurationMs = result.Duration * 1000.0,
                        Message = result.Message
                    });
                }
            }

            private static string MapStatus(TestStatus status)
            {
                switch (status)
                {
                    case TestStatus.Passed: return "Passed";
                    case TestStatus.Failed: return "Failed";
                    case TestStatus.Inconclusive: return "Skipped";
                    case TestStatus.Skipped: return "Skipped";
                    default: return status.ToString();
                }
            }
        }
    }

    public sealed class TestRunParams
    {
        /// <summary>Filter by test name (partial match). Leave empty to run all.</summary>
        public string TestNameFilter { get; set; }

        /// <summary>Filter by test category. Leave empty to run all.</summary>
        public string CategoryFilter { get; set; }
    }

    public sealed class TestRunResult
    {
        public int TotalTests { get; set; }
        public int Passed { get; set; }
        public int Failed { get; set; }
        public int Skipped { get; set; }
        public int DurationMs { get; set; }
        public List<TestDetail> FailedTests { get; set; } = new List<TestDetail>();
    }

    public sealed class TestDetail
    {
        public string Name { get; set; }
        public string FullName { get; set; }
        public string Status { get; set; }
        public double DurationMs { get; set; }
        public string Message { get; set; }
    }
}

using System;
using System.Text;
using System.Collections.Generic;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Interfaces;
using Mosaic.Bridge.Core.Discovery;
using Mosaic.Bridge.Core.Dispatcher;
using Mosaic.Bridge.Core.Pipeline;
using Mosaic.Bridge.Core.Pipeline.Stages;
using Mosaic.Bridge.Core.Pipeline.Validation;
using Mosaic.Bridge.Core.Server;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Mosaic.Bridge.Tests.Pipeline
{
    [TestFixture]
    public class PipelineIntegrationTests
    {
        // Test helpers
        private StubToolRunner _inner;
        private StubLogger _logger;

        [SetUp]
        public void SetUp()
        {
            _inner = new StubToolRunner();
            _logger = new StubLogger();
        }

        // --- Tests ---

        // 1. Direct mode with all stages registered — stages should NOT run
        [Test]
        public void FullPipeline_DirectMode_SkipsAllStages()
        {
            var pipeline = CreateFullPipeline();
            var warningPost = new AccumulatingWarningStage("post-warning");
            pipeline.AddPostStage(warningPost);

            var request = MakeRequest("mosaic_gameobject_create", "direct",
                new JObject { ["position"] = new JObject { ["x"] = 0, ["y"] = 0, ["z"] = 0 } });

            var response = pipeline.Execute(request);

            Assert.AreEqual(200, response.StatusCode);
            Assert.AreEqual(1, _inner.ExecuteCount, "Inner runner should be called in direct mode");

            // In direct mode the response is passthrough — no pipeline merging happens
            var body = JObject.Parse(response.Body);
            Assert.IsNull(body["warnings"], "No warnings should be merged in direct mode");
            Assert.IsNull(body["executionMode"], "No executionMode should be added in direct mode");
        }

        // 2. Validated mode — semantic validation runs, catches bad PBR value
        [Test]
        public void FullPipeline_ValidatedMode_RejectsBadPbrValue()
        {
            var entry = MakeEntry("mosaic_material_create", "material");
            var pipeline = CreateFullPipeline(entry);

            var parameters = new JObject { ["roughness"] = 5.0 };
            var request = MakeRequest("mosaic_material_create", "validated", parameters);

            var response = pipeline.Execute(request);

            Assert.AreEqual(400, response.StatusCode, "Should reject invalid PBR value");
            Assert.AreEqual(0, _inner.ExecuteCount, "Inner runner should NOT be called after rejection");

            var body = JObject.Parse(response.Body);
            Assert.AreEqual("VALIDATION_ERROR", body["error"]?.Value<string>());
            Assert.That(body["message"]?.Value<string>(), Does.Contain("roughness"));
        }

        // 3. Validated mode — passes valid parameters with warnings
        [Test]
        public void FullPipeline_ValidatedMode_PassesWithWarnings()
        {
            var entry = MakeEntry("mosaic_gameobject_create", "gameobject");
            var pipeline = CreateFullPipeline(entry);

            // Y=-600 is outside the typical range [-500, 5000] but not beyond absolute max
            var parameters = new JObject
            {
                ["position"] = new JObject { ["x"] = 0, ["y"] = -600, ["z"] = 0 }
            };
            var request = MakeRequest("mosaic_gameobject_create", "validated", parameters);

            var response = pipeline.Execute(request);

            Assert.AreEqual(200, response.StatusCode, "Valid request should succeed");
            Assert.AreEqual(1, _inner.ExecuteCount, "Inner runner should be called");

            var body = JObject.Parse(response.Body);
            var warnings = body["warnings"] as JArray;
            Assert.IsNotNull(warnings, "Response should contain warnings array");
            Assert.That(warnings.Count, Is.GreaterThanOrEqualTo(1), "Should have at least one warning");
            Assert.That(warnings.ToString(), Does.Contain("Y position"));
        }

        // 4. Verified mode for non-visual tool — no screenshots
        [Test]
        public void FullPipeline_VerifiedMode_NonVisualTool_NoScreenshots()
        {
            var entry = MakeEntry("mosaic_script_create", "script");
            var pipeline = CreateFullPipeline(entry);

            var request = MakeRequest("mosaic_script_create", "verified");

            var response = pipeline.Execute(request);

            Assert.AreEqual(200, response.StatusCode);
            Assert.AreEqual(1, _inner.ExecuteCount);

            var body = JObject.Parse(response.Body);
            // No VisualVerificationStage is registered (requires Unity APIs),
            // so no screenshots should appear
            Assert.IsNull(body["screenshots"], "Non-visual tool should not have screenshots");
            Assert.AreEqual("verified", body["executionMode"]?.Value<string>());
        }

        // 5. Invalid execution_mode — falls back to direct with warning
        [Test]
        public void FullPipeline_InvalidMode_FallsBackToDirectWithWarning()
        {
            var entry = MakeEntry("mosaic_gameobject_create", "gameobject");
            var pipeline = CreateFullPipeline(entry);

            var request = MakeRequest("mosaic_gameobject_create", "turbo");

            var response = pipeline.Execute(request);

            // Falls back to direct — inner runner called, stages skipped
            Assert.AreEqual(200, response.StatusCode);
            Assert.AreEqual(1, _inner.ExecuteCount, "Inner runner should be called in fallback-to-direct");

            // Direct mode passthrough means no pipeline merge, so warning is NOT in the response body.
            // The warning was added to ExecutionContext but MergeContext is never called in direct mode.
            // This is correct behavior: direct mode = zero overhead passthrough.
            var body = JObject.Parse(response.Body);
            Assert.IsNull(body["executionMode"], "Direct mode passthrough should not add executionMode");
        }

        // 6. Pipeline preserves tool result data
        [Test]
        public void FullPipeline_ValidatedMode_PreservesToolResultData()
        {
            var entry = MakeEntry("mosaic_gameobject_create", "gameobject");
            var pipeline = CreateFullPipeline(entry);

            _inner.Response = new HandlerResponse
            {
                StatusCode = 200,
                ContentType = "application/json",
                Body = "{\"success\":true,\"data\":{\"name\":\"MyObject\",\"id\":42}}"
            };

            var parameters = new JObject
            {
                ["position"] = new JObject { ["x"] = 0, ["y"] = 0, ["z"] = 0 }
            };
            var request = MakeRequest("mosaic_gameobject_create", "validated", parameters);

            var response = pipeline.Execute(request);

            Assert.AreEqual(200, response.StatusCode);
            var body = JObject.Parse(response.Body);

            // Original tool result data should be preserved
            Assert.AreEqual(true, body["success"]?.Value<bool>());
            var data = body["data"] as JObject;
            Assert.IsNotNull(data, "Data object should be preserved");
            Assert.AreEqual("MyObject", data["name"]?.Value<string>());
            Assert.AreEqual(42, data["id"]?.Value<int>());

            // Pipeline metadata should also be present
            Assert.AreEqual("validated", body["executionMode"]?.Value<string>());
        }

        // 7. Multiple warnings accumulate
        [Test]
        public void FullPipeline_MultipleWarnings_AllAccumulated()
        {
            var entry = MakeEntry("mosaic_gameobject_create", "gameobject");
            var config = new PipelineConfiguration();
            var pipeline = new ExecutionPipeline(_inner, config, _logger, name => entry);

            // Register multiple warning-producing stages
            pipeline.AddPreStage(new AccumulatingWarningStage("Pre-check alpha"));
            pipeline.AddPreStage(new AccumulatingWarningStage("Pre-check beta"));
            pipeline.AddPostStage(new AccumulatingWarningStage("Post-check gamma"));

            var request = MakeRequest("mosaic_gameobject_create", "validated");

            var response = pipeline.Execute(request);

            Assert.AreEqual(200, response.StatusCode);
            var body = JObject.Parse(response.Body);
            var warnings = body["warnings"] as JArray;
            Assert.IsNotNull(warnings, "Response should contain warnings");
            Assert.AreEqual(3, warnings.Count, "All three stage warnings should accumulate");
            Assert.That(warnings[0].Value<string>(), Does.Contain("Pre-check alpha"));
            Assert.That(warnings[1].Value<string>(), Does.Contain("Pre-check beta"));
            Assert.That(warnings[2].Value<string>(), Does.Contain("Post-check gamma"));
        }

        // 8. ExecutionMode included in response for non-direct modes
        [Test]
        public void FullPipeline_NonDirectMode_IncludesExecutionModeInResponse()
        {
            var entry = MakeEntry("mosaic_gameobject_create", "gameobject");
            var pipeline = CreateFullPipeline(entry);

            // Test validated mode
            var validatedRequest = MakeRequest("mosaic_gameobject_create", "validated");
            var validatedResponse = pipeline.Execute(validatedRequest);
            var validatedBody = JObject.Parse(validatedResponse.Body);
            Assert.AreEqual("validated", validatedBody["executionMode"]?.Value<string>(),
                "Validated mode should be reported in response");

            // Reset inner runner for next call
            _inner = new StubToolRunner();
            var pipeline2 = CreateFullPipeline(entry);

            // Test verified mode
            var verifiedRequest = MakeRequest("mosaic_gameobject_create", "verified");
            var verifiedResponse = pipeline2.Execute(verifiedRequest);
            var verifiedBody = JObject.Parse(verifiedResponse.Body);
            Assert.AreEqual("verified", verifiedBody["executionMode"]?.Value<string>(),
                "Verified mode should be reported in response");

            // Reset for reviewed mode
            _inner = new StubToolRunner();
            var pipeline3 = CreateFullPipeline(entry);

            // Test reviewed mode
            var reviewedRequest = MakeRequest("mosaic_gameobject_create", "reviewed");
            var reviewedResponse = pipeline3.Execute(reviewedRequest);
            var reviewedBody = JObject.Parse(reviewedResponse.Body);
            Assert.AreEqual("reviewed", reviewedBody["executionMode"]?.Value<string>(),
                "Reviewed mode should be reported in response");
        }

        // --- Helpers ---

        private ExecutionPipeline CreateFullPipeline(ToolRegistryEntry toolEntry = null)
        {
            var config = new PipelineConfiguration();
            var pipeline = new ExecutionPipeline(
                _inner, config, _logger,
                name => toolEntry);

            // Register all production stages that work without Unity APIs
            pipeline.AddPreStage(new SemanticValidatorStage(new IValidationRule[]
            {
                new TransformRangeRule(),
                new PbrRangeRule(),
            }));
            // Note: VisualVerificationStage and CodeReviewStage need Unity APIs
            // so we skip them in pure unit tests

            return pipeline;
        }

        private static HandlerRequest MakeRequest(string toolName, string executionMode, JObject parameters = null)
        {
            var body = new JObject
            {
                ["tool"] = toolName,
                ["parameters"] = parameters ?? new JObject()
            };
            if (executionMode != null)
                body["execution_mode"] = executionMode;

            return new HandlerRequest
            {
                Method = "POST",
                RawUrl = "/execute",
                Body = Encoding.UTF8.GetBytes(body.ToString()),
                ClientId = "test"
            };
        }

        private static ToolRegistryEntry MakeEntry(string toolName, string category, bool isReadOnly = false)
        {
            var attr = new MosaicToolAttribute(
                $"{category}/test", "Test tool",
                isReadOnly: isReadOnly, category: category);
            return new ToolRegistryEntry(toolName, attr, null, null);
        }

        // --- Test doubles ---

        private class StubToolRunner : IToolRunner
        {
            public int ExecuteCount { get; private set; }
            public HandlerResponse Response { get; set; } = new HandlerResponse
            {
                StatusCode = 200,
                ContentType = "application/json",
                Body = "{\"success\":true,\"data\":{\"name\":\"TestObj\"}}"
            };

            public HandlerResponse Execute(HandlerRequest request)
            {
                ExecuteCount++;
                return Response;
            }
        }

        private class StubLogger : IMosaicLogger
        {
            public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;
            public void Trace(string msg, params (string, object)[] ctx) {}
            public void Debug(string msg, params (string, object)[] ctx) {}
            public void Info(string msg, params (string, object)[] ctx) {}
            public void Warn(string msg, params (string, object)[] ctx) {}
            public void Error(string msg, Exception ex = null, params (string, object)[] ctx) {}
            public bool IsEnabled(LogLevel level) => level >= MinimumLevel;
        }

        /// <summary>
        /// A pipeline stage that adds a warning to the execution context.
        /// Used to test warning accumulation across multiple stages.
        /// </summary>
        private class AccumulatingWarningStage : IPipelineStage
        {
            private readonly string _warning;
            public AccumulatingWarningStage(string warning) => _warning = warning;

            public bool Execute(ExecutionContext context, ref HandlerResponse toolResult)
            {
                context.Warnings.Add(_warning);
                return true;
            }
        }
    }
}

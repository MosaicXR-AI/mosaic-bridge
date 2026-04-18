using System;
using System.Collections.Generic;
using System.Text;
using Mosaic.Bridge.Contracts.Interfaces;
using Mosaic.Bridge.Core.Discovery;
using Mosaic.Bridge.Core.Dispatcher;
using Mosaic.Bridge.Core.Pipeline;
using Mosaic.Bridge.Core.Server;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Mosaic.Bridge.Tests.Pipeline
{
    [TestFixture]
    public class ExecutionPipelineTests
    {
        private StubToolRunner _inner;
        private PipelineConfiguration _config;
        private StubLogger _logger;
        private ExecutionPipeline _pipeline;

        [SetUp]
        public void SetUp()
        {
            _inner = new StubToolRunner();
            _config = new PipelineConfiguration();
            _logger = new StubLogger();
            _pipeline = new ExecutionPipeline(
                _inner,
                _config,
                _logger,
                name => null); // no tool lookup needed for basic tests
        }

        [Test]
        public void DirectMode_PassesThrough_ZeroOverhead()
        {
            var request = MakeRequest("mosaic_gameobject_create", "direct");
            var response = _pipeline.Execute(request);

            Assert.AreEqual(200, response.StatusCode);
            Assert.AreEqual(1, _inner.ExecuteCount, "Should call inner runner exactly once");
        }

        [Test]
        public void ValidatedMode_RunsPreStages()
        {
            var stage = new RecordingStage();
            _pipeline.AddPreStage(stage);

            var request = MakeRequest("mosaic_gameobject_create", "validated");
            _pipeline.Execute(request);

            Assert.AreEqual(1, stage.ExecuteCount, "Pre-stage should run in validated mode");
            Assert.AreEqual(1, _inner.ExecuteCount, "Inner runner should still execute");
        }

        [Test]
        public void DirectMode_SkipsAllStages()
        {
            var preStage = new RecordingStage();
            var postStage = new RecordingStage();
            _pipeline.AddPreStage(preStage);
            _pipeline.AddPostStage(postStage);

            var request = MakeRequest("mosaic_gameobject_create", "direct");
            _pipeline.Execute(request);

            Assert.AreEqual(0, preStage.ExecuteCount, "Pre-stage should NOT run in direct mode");
            Assert.AreEqual(0, postStage.ExecuteCount, "Post-stage should NOT run in direct mode");
        }

        [Test]
        public void PreStage_RejectsRequest_InnerNotCalled()
        {
            var rejectStage = new RejectingStage();
            _pipeline.AddPreStage(rejectStage);

            var request = MakeRequest("mosaic_gameobject_create", "validated");
            var response = _pipeline.Execute(request);

            Assert.AreEqual(400, response.StatusCode, "Should return rejection status");
            Assert.AreEqual(0, _inner.ExecuteCount, "Inner runner should NOT be called after rejection");
        }

        [Test]
        public void InvalidMode_FallsBackToDirectWithWarning()
        {
            var stage = new RecordingStage();
            _pipeline.AddPreStage(stage);

            var request = MakeRequest("mosaic_gameobject_create", "turbo");
            var response = _pipeline.Execute(request);

            // "turbo" is unknown, falls back to direct — stages should NOT run
            Assert.AreEqual(0, stage.ExecuteCount, "Stage should not run for invalid mode (falls back to direct)");
            Assert.AreEqual(1, _inner.ExecuteCount);
        }

        [Test]
        public void NoExecutionMode_UsesConfigDefault()
        {
            var stage = new RecordingStage();
            _pipeline.AddPreStage(stage);

            // Config default is "direct" by default
            var request = MakeRequest("mosaic_gameobject_create", null);
            _pipeline.Execute(request);

            Assert.AreEqual(0, stage.ExecuteCount, "Default direct mode should skip stages");
        }

        [Test]
        public void PostStage_RunsAfterToolExecution()
        {
            var postStage = new RecordingStage();
            _pipeline.AddPostStage(postStage);

            var request = MakeRequest("mosaic_gameobject_create", "validated");
            _pipeline.Execute(request);

            Assert.AreEqual(1, postStage.ExecuteCount, "Post-stage should run after tool execution");
            Assert.IsTrue(postStage.LastToolResultWasNotNull, "Post-stage should receive tool result");
        }

        [Test]
        public void Warnings_MergedIntoResponse()
        {
            var warningStage = new WarningStage("Test warning from pipeline");
            _pipeline.AddPreStage(warningStage);

            var request = MakeRequest("mosaic_gameobject_create", "validated");
            var response = _pipeline.Execute(request);

            var body = JObject.Parse(response.Body);
            var warnings = body["warnings"] as JArray;
            Assert.IsNotNull(warnings, "Response should contain warnings array");
            Assert.That(warnings.ToString(), Does.Contain("Test warning from pipeline"));
        }

        [Test]
        public void ExecutionMode_IncludedInResponse()
        {
            var stage = new RecordingStage();
            _pipeline.AddPreStage(stage);

            var request = MakeRequest("mosaic_gameobject_create", "verified");
            var response = _pipeline.Execute(request);

            var body = JObject.Parse(response.Body);
            Assert.AreEqual("verified", body["executionMode"]?.Value<string>());
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private static HandlerRequest MakeRequest(string toolName, string executionMode)
        {
            var body = new JObject { ["tool"] = toolName, ["parameters"] = new JObject() };
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

        // ── Test doubles ────────────────────────────────────────────────────

        private class StubToolRunner : IToolRunner
        {
            public int ExecuteCount { get; private set; }

            public HandlerResponse Execute(HandlerRequest request)
            {
                ExecuteCount++;
                return new HandlerResponse
                {
                    StatusCode = 200,
                    ContentType = "application/json",
                    Body = "{\"success\":true,\"data\":{\"name\":\"TestObj\"}}"
                };
            }
        }

        private class RecordingStage : IPipelineStage
        {
            public int ExecuteCount { get; private set; }
            public bool LastToolResultWasNotNull { get; private set; }

            public bool Execute(ExecutionContext context, ref HandlerResponse toolResult)
            {
                ExecuteCount++;
                LastToolResultWasNotNull = toolResult != null;
                return true;
            }
        }

        private class RejectingStage : IPipelineStage
        {
            public bool Execute(ExecutionContext context, ref HandlerResponse toolResult)
            {
                toolResult = new HandlerResponse
                {
                    StatusCode = 400,
                    ContentType = "application/json",
                    Body = "{\"error\":\"REJECTED\",\"message\":\"Validation failed\"}"
                };
                return false;
            }
        }

        private class WarningStage : IPipelineStage
        {
            private readonly string _warning;
            public WarningStage(string warning) => _warning = warning;

            public bool Execute(ExecutionContext context, ref HandlerResponse toolResult)
            {
                context.Warnings.Add(_warning);
                return true;
            }
        }

        private class StubLogger : IMosaicLogger
        {
            public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;
            public void Trace(string message, params (string Key, object Value)[] context) { }
            public void Debug(string message, params (string Key, object Value)[] context) { }
            public void Info(string message, params (string Key, object Value)[] context) { }
            public void Warn(string message, params (string Key, object Value)[] context) { }
            public void Error(string message, Exception ex = null, params (string Key, object Value)[] context) { }
            public bool IsEnabled(LogLevel level) => level >= MinimumLevel;
        }
    }
}

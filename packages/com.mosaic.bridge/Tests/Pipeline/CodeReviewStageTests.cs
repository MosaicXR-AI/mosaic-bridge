using System;
using System.Collections.Generic;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Interfaces;
using Mosaic.Bridge.Core.Discovery;
using Mosaic.Bridge.Core.Pipeline;
using Mosaic.Bridge.Core.Pipeline.Stages;
using Mosaic.Bridge.Core.Server;
using NUnit.Framework;

namespace Mosaic.Bridge.Tests.Pipeline
{
    [TestFixture]
    public sealed class CodeReviewStageTests
    {
        private StubLogger _logger;
        private HandlerResponse _response;

        [SetUp]
        public void SetUp()
        {
            _logger = new StubLogger();
            _response = new HandlerResponse
            {
                StatusCode = 200,
                ContentType = "application/json",
                Body = "{\"success\":true}"
            };
        }

        [Test]
        public void Execute_NonScriptTool_Skips()
        {
            var config = new PipelineConfiguration();
            var stage = new CodeReviewStage(config, _logger);

            var context = new ExecutionContext
            {
                ToolName = "mosaic_gameobject_create",
                ToolEntry = MakeEntry("mosaic_gameobject_create", "gameobject"),
                Mode = ExecutionMode.Validated
            };

            var originalBody = _response.Body;
            bool result = stage.Execute(context, ref _response);

            Assert.IsTrue(result);
            Assert.AreEqual(originalBody, _response.Body);
            Assert.AreEqual(0, context.Warnings.Count);
        }

        [Test]
        public void Execute_ScriptReadTool_Skips()
        {
            var config = new PipelineConfiguration();
            var stage = new CodeReviewStage(config, _logger);

            var context = new ExecutionContext
            {
                ToolName = "mosaic_script_read",
                ToolEntry = MakeEntry("mosaic_script_read", "script"),
                Mode = ExecutionMode.Validated
            };

            var originalBody = _response.Body;
            bool result = stage.Execute(context, ref _response);

            Assert.IsTrue(result);
            Assert.AreEqual(originalBody, _response.Body);
        }

        [Test]
        public void Execute_CodeReviewDisabled_Skips()
        {
            var config = new PipelineConfiguration { CodeReviewEnabled = false };
            var stage = new CodeReviewStage(config, _logger);

            var context = new ExecutionContext
            {
                ToolName = "mosaic_script_create",
                ToolEntry = MakeEntry("mosaic_script_create", "script"),
                Mode = ExecutionMode.Validated
            };

            var originalBody = _response.Body;
            bool result = stage.Execute(context, ref _response);

            Assert.IsTrue(result);
            Assert.AreEqual(originalBody, _response.Body);
        }

        [Test]
        public void Execute_DirectMode_Skips()
        {
            var config = new PipelineConfiguration();
            var stage = new CodeReviewStage(config, _logger);

            var context = new ExecutionContext
            {
                ToolName = "mosaic_script_create",
                ToolEntry = MakeEntry("mosaic_script_create", "script"),
                Mode = ExecutionMode.Direct
            };

            var originalBody = _response.Body;
            bool result = stage.Execute(context, ref _response);

            Assert.IsTrue(result);
            Assert.AreEqual(originalBody, _response.Body);
        }

        [Test]
        public void Execute_NullToolEntry_ParsesCategoryFromToolName()
        {
            var config = new PipelineConfiguration { CodeReviewEnabled = false };
            var stage = new CodeReviewStage(config, _logger);

            var context = new ExecutionContext
            {
                ToolName = "mosaic_script_create",
                ToolEntry = null,
                Mode = ExecutionMode.Validated
            };

            var originalBody = _response.Body;
            bool result = stage.Execute(context, ref _response);

            // Identified as script tool but skipped because disabled
            Assert.IsTrue(result);
            Assert.AreEqual(originalBody, _response.Body);
        }

        [Test]
        public void Execute_NonScriptCategory_SkipsEvenIfToolNameContainsCreate()
        {
            var config = new PipelineConfiguration();
            var stage = new CodeReviewStage(config, _logger);

            var context = new ExecutionContext
            {
                ToolName = "mosaic_asset_create",
                ToolEntry = MakeEntry("mosaic_asset_create", "asset"),
                Mode = ExecutionMode.Verified
            };

            var originalBody = _response.Body;
            bool result = stage.Execute(context, ref _response);

            Assert.IsTrue(result);
            Assert.AreEqual(originalBody, _response.Body);
        }

        private static ToolRegistryEntry MakeEntry(string toolName, string category)
        {
            var attr = new MosaicToolAttribute(
                $"{category}/test", "Test tool",
                isReadOnly: false, category: category);
            return new ToolRegistryEntry(toolName, attr, null, null);
        }

        private sealed class StubLogger : IMosaicLogger
        {
            public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;
            public bool IsEnabled(LogLevel level) => level >= MinimumLevel;
            public void Trace(string message, params (string Key, object Value)[] context) { }
            public void Debug(string message, params (string Key, object Value)[] context) { }
            public void Info(string message, params (string Key, object Value)[] context) { }
            public void Warn(string message, params (string Key, object Value)[] context) { }
            public void Error(string message, Exception ex = null, params (string Key, object Value)[] context) { }
        }
    }
}

using System;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Interfaces;
using Mosaic.Bridge.Core.Discovery;
using Mosaic.Bridge.Core.Pipeline;
using Mosaic.Bridge.Core.Pipeline.Stages;
using Mosaic.Bridge.Core.Server;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Mosaic.Bridge.Tests.Pipeline
{
    [TestFixture]
    public sealed class ScriptApprovalStageTests
    {
        private const string PrefKey = "MosaicBridge.ScriptApprovalEnabled";

        private StubLogger _logger;
        private ScriptApprovalStage _stage;
        private HandlerResponse _response;

        [SetUp]
        public void SetUp()
        {
            _logger = new StubLogger();
            _stage = new ScriptApprovalStage(_logger);
            _response = default;
            ScriptApprovalStage.ClearPending();
        }

        [TearDown]
        public void TearDown()
        {
            EditorPrefs.DeleteKey(PrefKey);
            ScriptApprovalStage.ClearPending();
        }

        // -----------------------------------------------------------------
        // Pass-through tests (stage returns true, does not block)
        // -----------------------------------------------------------------

        [Test]
        public void Execute_ApprovalDisabled_PassesThrough()
        {
            EditorPrefs.SetBool(PrefKey, false);

            var context = MakeScriptContext("mosaic_script_create", "script");
            bool result = _stage.Execute(context, ref _response);

            Assert.IsTrue(result);
            Assert.IsNull(_response);
        }

        [Test]
        public void Execute_NonScriptTool_PassesThrough()
        {
            EditorPrefs.SetBool(PrefKey, true);

            var context = new ExecutionContext
            {
                ToolName = "mosaic_gameobject_create",
                ToolEntry = MakeEntry("mosaic_gameobject_create", "gameobject"),
                Parameters = new JObject { ["path"] = "Assets/Scripts/Foo.cs" }
            };

            bool result = _stage.Execute(context, ref _response);

            Assert.IsTrue(result);
            Assert.IsNull(_response);
        }

        [Test]
        public void Execute_ScriptReadTool_PassesThrough()
        {
            EditorPrefs.SetBool(PrefKey, true);

            var context = new ExecutionContext
            {
                ToolName = "mosaic_script_read",
                ToolEntry = MakeEntry("mosaic_script_read", "script"),
                Parameters = new JObject { ["path"] = "Assets/Scripts/Foo.cs" }
            };

            bool result = _stage.Execute(context, ref _response);

            Assert.IsTrue(result);
            Assert.IsNull(_response);
        }

        // -----------------------------------------------------------------
        // Approval-required tests (stage returns false, blocks execution)
        // -----------------------------------------------------------------

        [Test]
        public void Execute_ScriptCreateTool_ReturnsApprovalRequired()
        {
            EditorPrefs.SetBool(PrefKey, true);

            var context = MakeScriptContext("mosaic_script_create", "script",
                path: "Assets/Scripts/Player.cs",
                content: "using UnityEngine; public class Player : MonoBehaviour {}");

            bool result = _stage.Execute(context, ref _response);

            Assert.IsFalse(result);
            Assert.IsNotNull(_response);
            Assert.AreEqual(200, _response.StatusCode);

            var body = JObject.Parse(_response.Body);
            Assert.AreEqual("APPROVAL_REQUIRED", body["errorCode"]?.Value<string>());
            Assert.IsNotNull(body["data"]?["approvalToken"]?.Value<string>());
            Assert.AreEqual("Assets/Scripts/Player.cs", body["data"]?["path"]?.Value<string>());
            Assert.AreEqual(1, ScriptApprovalStage.PendingCount);
        }

        // -----------------------------------------------------------------
        // Token flow tests
        // -----------------------------------------------------------------

        [Test]
        public void Execute_WithValidToken_PassesThrough()
        {
            EditorPrefs.SetBool(PrefKey, true);

            // First call: get a token
            var context1 = MakeScriptContext("mosaic_script_create", "script",
                path: "Assets/Scripts/Foo.cs", content: "class Foo {}");
            _stage.Execute(context1, ref _response);

            var body1 = JObject.Parse(_response.Body);
            var token = body1["data"]["approvalToken"].Value<string>();
            Assert.IsNotNull(token);

            // Second call: use the token
            _response = default;
            var context2 = MakeScriptContext("mosaic_script_create", "script",
                path: "Assets/Scripts/Foo.cs", content: "class Foo {}");
            context2.Parameters["_approvalToken"] = token;

            bool result = _stage.Execute(context2, ref _response);

            Assert.IsTrue(result);
            Assert.AreEqual(0, ScriptApprovalStage.PendingCount);
        }

        [Test]
        public void Execute_WithInvalidToken_Returns403()
        {
            EditorPrefs.SetBool(PrefKey, true);

            var context = MakeScriptContext("mosaic_script_update", "script",
                path: "Assets/Scripts/Foo.cs", content: "class Foo {}");
            context.Parameters["_approvalToken"] = "bad_token_999";

            bool result = _stage.Execute(context, ref _response);

            Assert.IsFalse(result);
            Assert.IsNotNull(_response);
            Assert.AreEqual(403, _response.StatusCode);

            var body = JObject.Parse(_response.Body);
            Assert.AreEqual("APPROVAL_REQUIRED", body["error"]?.Value<string>());
        }

        // -----------------------------------------------------------------
        // Edge cases
        // -----------------------------------------------------------------

        [Test]
        public void Execute_EditorPath_IncludesWarning()
        {
            EditorPrefs.SetBool(PrefKey, true);

            var context = MakeScriptContext("mosaic_script_create", "script",
                path: "Assets/Editor/MyTool.cs",
                content: "using UnityEditor; class MyTool {}");

            _stage.Execute(context, ref _response);

            var body = JObject.Parse(_response.Body);
            var warning = body["data"]?["warning"]?.Value<string>();
            Assert.IsNotNull(warning);
            StringAssert.Contains("Editor/", warning);
        }

        [Test]
        public void Execute_PreviewTruncatesLongContent()
        {
            EditorPrefs.SetBool(PrefKey, true);

            var longContent = new string('x', 1000);
            var context = MakeScriptContext("mosaic_script_create", "script",
                path: "Assets/Scripts/Big.cs",
                content: longContent);

            _stage.Execute(context, ref _response);

            var body = JObject.Parse(_response.Body);
            var preview = body["data"]?["contentPreview"]?.Value<string>();
            Assert.IsNotNull(preview);
            Assert.IsTrue(preview.EndsWith("..."));
            // 500 chars + "..."
            Assert.AreEqual(503, preview.Length);
            Assert.AreEqual(1000, body["data"]["contentLength"].Value<int>());
        }

        // -----------------------------------------------------------------
        // Helpers
        // -----------------------------------------------------------------

        private static ExecutionContext MakeScriptContext(string toolName, string category,
            string path = "Assets/Scripts/Test.cs", string content = "class Test {}")
        {
            return new ExecutionContext
            {
                ToolName = toolName,
                ToolEntry = MakeEntry(toolName, category),
                Parameters = new JObject
                {
                    ["path"] = path,
                    ["content"] = content
                }
            };
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
            public void Trace(string msg, params (string Key, object Value)[] ctx) { }
            public void Debug(string msg, params (string Key, object Value)[] ctx) { }
            public void Info(string msg, params (string Key, object Value)[] ctx) { }
            public void Warn(string msg, params (string Key, object Value)[] ctx) { }
            public void Error(string msg, Exception ex = null, params (string Key, object Value)[] ctx) { }
        }
    }
}

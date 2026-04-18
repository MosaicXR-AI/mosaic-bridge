using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Interfaces;
using Mosaic.Bridge.Core.Discovery;
using Mosaic.Bridge.Core.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Mosaic.Bridge.Tests.Discovery
{
    [TestFixture]
    public class ToolRegistryTests
    {
        // ── Test fixture param types ─────────────────────────────────────────────

        internal class TestReadOnlyParams { public string Value { get; set; } }
        internal class TestWriteParams { [RequiredAttribute] public int Count { get; set; } }

        // ── Test fixture tool methods (decorated with [MosaicTool]) ───────────────

        [MosaicTool("test/readonly", "A read-only test tool", isReadOnly: true)]
        internal static ToolResult<string> ReadOnlyTool(TestReadOnlyParams p) =>
            ToolResult<string>.Ok(p.Value ?? "default");

        [MosaicTool("test/write", "A write test tool", isReadOnly: false)]
        internal static ToolResult<int> WriteTool(TestWriteParams p) =>
            ToolResult<int>.Ok(p.Count);

        [MosaicTool("test/throws", "A tool that throws", isReadOnly: false)]
        internal static ToolResult<string> ThrowingTool(TestReadOnlyParams p) =>
            throw new InvalidOperationException("intentional failure");

        // NOT decorated — must not be registered
        internal static ToolResult<string> NotATool(TestReadOnlyParams p) =>
            ToolResult<string>.Ok("should not appear");

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static ToolRegistry BuildTestRegistry()
        {
            var methods = typeof(ToolRegistryTests)
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(m => m.GetCustomAttribute<MosaicToolAttribute>() != null)
                .ToList();

            var entries = new List<ToolRegistryEntry>();
            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<MosaicToolAttribute>();
                var parameters = method.GetParameters();
                var paramType = parameters.Length > 0 ? parameters[0].ParameterType : null;
                var toolName = "mosaic_" + attr.Route;
                entries.Add(new ToolRegistryEntry(toolName, attr, method, paramType));
            }

            return new ToolRegistry(entries, new NullLogger());
        }

        private static HandlerRequest MakePost(object bodyObj)
        {
            var json = JsonConvert.SerializeObject(bodyObj);
            return new HandlerRequest
            {
                Method = "POST",
                RawUrl = "/execute",
                Body = Encoding.UTF8.GetBytes(json)
            };
        }

        private static HandlerRequest MakeGet(string url) =>
            new HandlerRequest { Method = "GET", RawUrl = url, Body = new byte[0] };

        // ── Build tests ──────────────────────────────────────────────────────────

        [Test]
        public void Build_RegistersDecoratedMethods_ExcludesUndecorated()
        {
            var registry = BuildTestRegistry();
            var response = registry.Execute(MakeGet("/tools"));

            var body = JObject.Parse(response.Body);
            var tools = body["tools"] as JArray;

            Assert.IsNotNull(tools);
            Assert.AreEqual(3, tools.Count);

            var names = tools.Select(t => t["name"].Value<string>()).ToList();
            Assert.IsFalse(names.Any(n => n.Contains("NotATool")));
        }

        [Test]
        public void Build_ToolNamesHaveMosaicPrefix()
        {
            var registry = BuildTestRegistry();
            var response = registry.Execute(MakeGet("/tools"));

            var body = JObject.Parse(response.Body);
            var tools = body["tools"] as JArray;

            Assert.IsNotNull(tools);
            foreach (var tool in tools)
                StringAssert.StartsWith("mosaic_", tool["name"].Value<string>());
        }

        // ── Execute tests ────────────────────────────────────────────────────────

        [Test]
        public void Execute_UnknownTool_Returns404()
        {
            var registry = BuildTestRegistry();
            var response = registry.Execute(MakePost(new { tool = "mosaic_no/such_tool", parameters = new { } }));

            Assert.AreEqual(404, response.StatusCode);
        }

        [Test]
        public void Execute_ValidTool_Returns200WithResult()
        {
            var registry = BuildTestRegistry();
            var response = registry.Execute(MakePost(new
            {
                tool = "mosaic_test/readonly",
                parameters = new { Value = "hello" }
            }));

            Assert.AreEqual(200, response.StatusCode);
            Assert.AreEqual("application/json", response.ContentType);

            var body = JObject.Parse(response.Body);
            Assert.IsTrue(body["success"].Value<bool>());
            Assert.AreEqual("hello", body["data"].Value<string>());
        }

        [Test]
        public void Execute_MissingRequiredParam_Returns400()
        {
            // No "parameters" field → ParameterValidator returns INVALID_PARAM
            var registry = BuildTestRegistry();
            var response = registry.Execute(MakePost(new { tool = "mosaic_test/write" }));

            Assert.AreEqual(400, response.StatusCode);
        }

        [Test]
        public void Execute_ToolThrows_Returns500()
        {
            var registry = BuildTestRegistry();
            var response = registry.Execute(MakePost(new
            {
                tool = "mosaic_test/throws",
                parameters = new { Value = "anything" }
            }));

            Assert.AreEqual(500, response.StatusCode);
            StringAssert.Contains("intentional failure", response.Body);
        }

        [Test]
        public void Execute_GetTools_Returns200WithList()
        {
            var registry = BuildTestRegistry();
            var response = registry.Execute(MakeGet("/tools"));

            Assert.AreEqual(200, response.StatusCode);
            Assert.AreEqual("application/json", response.ContentType);

            var body = JObject.Parse(response.Body);
            Assert.IsNotNull(body["tools"]);
        }

        [Test]
        public void Execute_GetTools_ListContainsRegisteredTools()
        {
            var registry = BuildTestRegistry();
            var response = registry.Execute(MakeGet("/tools"));

            var body = JObject.Parse(response.Body);
            var tools = body["tools"] as JArray;

            Assert.IsNotNull(tools);
            var names = tools.Select(t => t["name"].Value<string>()).ToList();

            Assert.Contains("mosaic_test/readonly", names);
            Assert.Contains("mosaic_test/write", names);
            Assert.Contains("mosaic_test/throws", names);
        }

        [Test]
        public void Execute_UnknownRoute_Returns404()
        {
            var registry = BuildTestRegistry();
            var response = registry.Execute(new HandlerRequest
            {
                Method = "PUT",
                RawUrl = "/something",
                Body = new byte[0]
            });

            Assert.AreEqual(404, response.StatusCode);
        }

        // ── Test doubles ─────────────────────────────────────────────────────────

        private sealed class NullLogger : IMosaicLogger
        {
            public LogLevel MinimumLevel { get; set; } = LogLevel.None;
            public void Trace(string message, params (string Key, object Value)[] context) { }
            public void Debug(string message, params (string Key, object Value)[] context) { }
            public void Info(string message, params (string Key, object Value)[] context) { }
            public void Warn(string message, params (string Key, object Value)[] context) { }
            public void Error(string message, Exception exception = null, params (string Key, object Value)[] context) { }
            public bool IsEnabled(LogLevel level) => false;
        }
    }
}

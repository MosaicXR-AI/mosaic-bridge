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
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Mosaic.Bridge.Tests.Bootstrap
{
    [TestFixture]
    public class ToolRegistryHealthTests
    {
        // ── Minimal fixture tool method ──────────────────────────────────────────

        internal class HealthTestParams { public string Value { get; set; } }

        [MosaicTool("health/test", "A minimal tool for health endpoint tests", isReadOnly: true)]
        internal static ToolResult<string> HealthTestTool(HealthTestParams p) =>
            ToolResult<string>.Ok(p.Value ?? "ok");

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static ToolRegistry BuildTestRegistry()
        {
            var methods = typeof(ToolRegistryHealthTests)
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

        private static HandlerRequest MakeGet(string url) =>
            new HandlerRequest { Method = "GET", RawUrl = url, Body = new byte[0] };

        // ── Health endpoint tests ────────────────────────────────────────────────

        [Test]
        public void Execute_GetHealth_Returns200()
        {
            var registry = BuildTestRegistry();
            var response = registry.Execute(MakeGet("/health"));

            Assert.AreEqual(200, response.StatusCode);
            Assert.AreEqual("application/json", response.ContentType);
        }

        [Test]
        public void Execute_GetHealth_ContainsStatusOk()
        {
            var registry = BuildTestRegistry();
            var response = registry.Execute(MakeGet("/health"));

            var body = JObject.Parse(response.Body);
            Assert.AreEqual("ok", body["status"].Value<string>());
        }

        [Test]
        public void Execute_GetHealth_ContainsToolCount()
        {
            var registry = BuildTestRegistry();
            var response = registry.Execute(MakeGet("/health"));

            var body = JObject.Parse(response.Body);
            Assert.AreEqual(1, body["tool_count"].Value<int>());
        }

        [Test]
        public void Execute_GetHealth_ContainsBridgeState()
        {
            var registry = BuildTestRegistry();
            var response = registry.Execute(MakeGet("/health"));

            var body = JObject.Parse(response.Body);
            var bridgeState = body["bridge_state"].Value<string>();
            Assert.IsFalse(string.IsNullOrEmpty(bridgeState));
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

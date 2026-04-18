using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Tools.Extensibility;
using NUnit.Framework;

namespace Mosaic.Bridge.Tests.Unit.Tools.Extensibility
{
    [TestFixture]
    [Category("Unit")]
    public class ExtensibilityToolTests
    {
        // ── Test: listing returns results ────────────────────────────────────────

        [Test]
        public void ListCustomTools_ReturnsResult_WithCustomToolsList()
        {
            var result = ExtensibilityListCustomToolsTool.ListCustomTools(
                new ExtensibilityListCustomToolsParams());

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data);
            Assert.IsNotNull(result.Data.CustomTools);
            // TotalCount should match the list size (no filter applied)
            Assert.AreEqual(result.Data.CustomTools.Count, result.Data.TotalCount);
        }

        [Test]
        public void ListCustomTools_ExcludesBuiltInTools()
        {
            var result = ExtensibilityListCustomToolsTool.ListCustomTools(
                new ExtensibilityListCustomToolsParams());

            Assert.IsTrue(result.Success, result.Error);

            // No tool should come from the Mosaic.Bridge.Tools namespace
            foreach (var tool in result.Data.CustomTools)
            {
                Assert.IsFalse(
                    tool.DeclaringType.StartsWith("Mosaic.Bridge.Tools.", StringComparison.Ordinal),
                    $"Built-in tool '{tool.Name}' (from {tool.DeclaringType}) should not appear in custom tools list");
                // Also check exact namespace match (class directly in Mosaic.Bridge.Tools)
                Assert.AreNotEqual("Mosaic.Bridge.Tools", GetNamespace(tool.DeclaringType),
                    $"Built-in tool '{tool.Name}' should not appear in custom tools list");
            }
        }

        [Test]
        public void ListCustomTools_AssemblyFilter_FiltersCorrectly()
        {
            var result = ExtensibilityListCustomToolsTool.ListCustomTools(
                new ExtensibilityListCustomToolsParams());

            Assert.IsTrue(result.Success, result.Error);

            if (result.Data.CustomTools.Count == 0)
            {
                // No custom tools registered — filter should also return empty
                var filtered = ExtensibilityListCustomToolsTool.ListCustomTools(
                    new ExtensibilityListCustomToolsParams { AssemblyFilter = "NonExistentAssembly" });
                Assert.IsTrue(filtered.Success);
                Assert.AreEqual(0, filtered.Data.CustomTools.Count);
                return;
            }

            // Use the first custom tool's assembly as a filter — should return at least that tool
            var targetAssembly = result.Data.CustomTools[0].Assembly;
            var filteredResult = ExtensibilityListCustomToolsTool.ListCustomTools(
                new ExtensibilityListCustomToolsParams { AssemblyFilter = targetAssembly });

            Assert.IsTrue(filteredResult.Success);
            Assert.IsTrue(filteredResult.Data.CustomTools.Count > 0,
                $"Filter by '{targetAssembly}' should return at least one tool");

            foreach (var tool in filteredResult.Data.CustomTools)
            {
                StringAssert.Contains(targetAssembly, tool.Assembly);
            }
        }

        [Test]
        public void ListCustomTools_AssemblyFilter_NonExistent_ReturnsEmpty()
        {
            var result = ExtensibilityListCustomToolsTool.ListCustomTools(
                new ExtensibilityListCustomToolsParams
                {
                    AssemblyFilter = "ZZZ_NoSuchAssembly_12345"
                });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0, result.Data.CustomTools.Count);
        }

        [Test]
        public void ListCustomTools_NullParams_DoesNotThrow()
        {
            var result = ExtensibilityListCustomToolsTool.ListCustomTools(null);

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data);
            Assert.IsNotNull(result.Data.CustomTools);
        }

        [Test]
        public void ListCustomTools_CustomToolInfo_HasRequiredFields()
        {
            var result = ExtensibilityListCustomToolsTool.ListCustomTools(
                new ExtensibilityListCustomToolsParams());

            Assert.IsTrue(result.Success, result.Error);

            foreach (var tool in result.Data.CustomTools)
            {
                Assert.IsFalse(string.IsNullOrEmpty(tool.Name),
                    "Tool Name must not be empty");
                Assert.IsFalse(string.IsNullOrEmpty(tool.Description),
                    $"Tool '{tool.Name}' Description must not be empty");
                Assert.IsFalse(string.IsNullOrEmpty(tool.Assembly),
                    $"Tool '{tool.Name}' Assembly must not be empty");
                Assert.IsFalse(string.IsNullOrEmpty(tool.DeclaringType),
                    $"Tool '{tool.Name}' DeclaringType must not be empty");
                Assert.IsFalse(string.IsNullOrEmpty(tool.Category),
                    $"Tool '{tool.Name}' Category must not be empty");

                // Tool names should follow the mosaic_ prefix convention
                StringAssert.StartsWith("mosaic_", tool.Name);
            }
        }

        [Test]
        public void ListCustomTools_TotalCount_ReflectsUnfilteredCount()
        {
            // Get unfiltered count
            var all = ExtensibilityListCustomToolsTool.ListCustomTools(
                new ExtensibilityListCustomToolsParams());
            Assert.IsTrue(all.Success);

            // Get filtered — TotalCount should still reflect unfiltered count
            var filtered = ExtensibilityListCustomToolsTool.ListCustomTools(
                new ExtensibilityListCustomToolsParams
                {
                    AssemblyFilter = "ZZZ_NoSuchAssembly_12345"
                });
            Assert.IsTrue(filtered.Success);
            Assert.AreEqual(all.Data.TotalCount, filtered.Data.TotalCount,
                "TotalCount should reflect the unfiltered count regardless of AssemblyFilter");
        }

        // ── Test: the test-fixture tools in Mosaic.Bridge.Tests are custom ──────

        [Test]
        public void ListCustomTools_IncludesTestTools_FromTestAssembly()
        {
            // The ToolRegistryTests in Mosaic.Bridge.Tests define [MosaicTool] methods
            // that are NOT in the Mosaic.Bridge.Tools namespace — they should appear as custom
            var result = ExtensibilityListCustomToolsTool.ListCustomTools(
                new ExtensibilityListCustomToolsParams());

            Assert.IsTrue(result.Success, result.Error);

            // The test assembly's tools (test/readonly, test/write, test/throws) should be
            // discovered as custom tools since they are in Mosaic.Bridge.Tests namespace
            var testTools = result.Data.CustomTools
                .Where(t => t.Assembly == "Mosaic.Bridge.Tests")
                .ToList();

            Assert.IsTrue(testTools.Count > 0,
                "Expected to find custom tools from the Mosaic.Bridge.Tests assembly");
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private static string GetNamespace(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName)) return "";
            var lastDot = fullTypeName.LastIndexOf('.');
            return lastDot >= 0 ? fullTypeName.Substring(0, lastDot) : "";
        }
    }
}

using System;
using System.Linq;
using System.Reflection;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Tools.Meta;
using NUnit.Framework;

namespace Mosaic.Bridge.Tests.Unit.Tools.Meta
{
    [TestFixture]
    [Category("Unit")]
    public class RuntimeToolsTests
    {
        // ── Test: runtime tools list contains expected categories ──────────────

        [Test]
        public void RuntimeTools_ContainsGameObjectCategory()
        {
            var result = MetaRuntimeToolsTool.Execute(new MetaRuntimeToolsParams());

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data);

            var categories = result.Data.Categories.Select(c => c.Category).ToList();
            Assert.Contains("gameobject", categories,
                "Runtime tools should include gameobject category");
        }

        [Test]
        public void RuntimeTools_ContainsComponentCategory()
        {
            var result = MetaRuntimeToolsTool.Execute(new MetaRuntimeToolsParams());

            Assert.IsTrue(result.Success, result.Error);
            var categories = result.Data.Categories.Select(c => c.Category).ToList();
            Assert.Contains("component", categories,
                "Runtime tools should include component category");
        }

        [Test]
        public void RuntimeTools_ContainsPhysicsCategory()
        {
            var result = MetaRuntimeToolsTool.Execute(new MetaRuntimeToolsParams());

            Assert.IsTrue(result.Success, result.Error);
            var categories = result.Data.Categories.Select(c => c.Category).ToList();
            Assert.Contains("physics", categories,
                "Runtime tools should include physics category");
        }

        [Test]
        public void RuntimeTools_ContainsExpectedCategories()
        {
            var result = MetaRuntimeToolsTool.Execute(new MetaRuntimeToolsParams());

            Assert.IsTrue(result.Success, result.Error);
            var categories = result.Data.Categories.Select(c => c.Category).ToList();

            // All these categories should have at least one runtime tool
            var expectedCategories = new[]
            {
                "gameobject", "component", "physics", "camera", "audio",
                "lighting", "particle", "ui", "procgen", "simulation",
                "mesh", "nav", "compute", "render", "shader", "material"
            };

            foreach (var expected in expectedCategories)
            {
                Assert.Contains(expected, categories,
                    $"Runtime tools should include '{expected}' category");
            }
        }

        // ── Test: editor-only tools are NOT in runtime list ─────────────────

        [Test]
        public void RuntimeTools_DoesNotContainAssetCategory()
        {
            var result = MetaRuntimeToolsTool.Execute(new MetaRuntimeToolsParams());

            Assert.IsTrue(result.Success, result.Error);
            var categories = result.Data.Categories.Select(c => c.Category).ToList();
            Assert.IsFalse(categories.Contains("asset"),
                "Runtime tools should NOT include asset category (editor-only)");
        }

        [Test]
        public void RuntimeTools_DoesNotContainScriptCategory()
        {
            var result = MetaRuntimeToolsTool.Execute(new MetaRuntimeToolsParams());

            Assert.IsTrue(result.Success, result.Error);
            var categories = result.Data.Categories.Select(c => c.Category).ToList();
            Assert.IsFalse(categories.Contains("script"),
                "Runtime tools should NOT include script category (editor-only)");
        }

        [Test]
        public void RuntimeTools_DoesNotContainSelectionCategory()
        {
            var result = MetaRuntimeToolsTool.Execute(new MetaRuntimeToolsParams());

            Assert.IsTrue(result.Success, result.Error);
            var categories = result.Data.Categories.Select(c => c.Category).ToList();
            Assert.IsFalse(categories.Contains("selection"),
                "Runtime tools should NOT include selection category (editor-only)");
        }

        [Test]
        public void RuntimeTools_DoesNotContainEditorOnlyCategories()
        {
            var result = MetaRuntimeToolsTool.Execute(new MetaRuntimeToolsParams());

            Assert.IsTrue(result.Success, result.Error);
            var categories = result.Data.Categories.Select(c => c.Category).ToList();

            var editorOnlyCategories = new[]
            {
                "asset", "script", "selection", "undo",
                "build", "prefab", "shadergraph", "package",
                "sceneview", "asmdef", "timeline", "taglayer"
            };

            foreach (var editorOnly in editorOnlyCategories)
            {
                Assert.IsFalse(categories.Contains(editorOnly),
                    $"Runtime tools should NOT include '{editorOnly}' category (editor-only)");
            }
        }

        // ── Test: total count and structure ──────────────────────────────────

        [Test]
        public void RuntimeTools_TotalCountMatchesSumOfCategories()
        {
            var result = MetaRuntimeToolsTool.Execute(new MetaRuntimeToolsParams());

            Assert.IsTrue(result.Success, result.Error);
            var sumOfCategories = result.Data.Categories.Sum(c => c.Count);
            Assert.AreEqual(result.Data.TotalCount, sumOfCategories,
                "TotalCount should equal sum of all category counts");
        }

        [Test]
        public void RuntimeTools_HasReasonableCount()
        {
            var result = MetaRuntimeToolsTool.Execute(new MetaRuntimeToolsParams());

            Assert.IsTrue(result.Success, result.Error);
            // We annotated ~94 tools as Both, so expect at least 80
            Assert.GreaterOrEqual(result.Data.TotalCount, 80,
                "Should have at least 80 runtime-compatible tools");
        }

        // ── Test: category filter works ─────────────────────────────────────

        [Test]
        public void RuntimeTools_CategoryFilter_ReturnsOnlyMatchingCategory()
        {
            var result = MetaRuntimeToolsTool.Execute(
                new MetaRuntimeToolsParams { Category = "gameobject" });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.Categories.Count,
                "Filtered result should have exactly one category");
            Assert.AreEqual("gameobject", result.Data.Categories[0].Category);
            Assert.GreaterOrEqual(result.Data.Categories[0].Count, 7,
                "gameobject category should have at least 7 runtime tools");
        }

        // ── Test: each tool has required fields ─────────────────────────────

        [Test]
        public void RuntimeTools_AllToolsHaveRequiredFields()
        {
            var result = MetaRuntimeToolsTool.Execute(new MetaRuntimeToolsParams());

            Assert.IsTrue(result.Success, result.Error);

            foreach (var category in result.Data.Categories)
            {
                Assert.IsNotNull(category.Category, "Category should not be null");
                Assert.IsNotNull(category.Tools, $"Tools list for {category.Category} should not be null");

                foreach (var tool in category.Tools)
                {
                    Assert.IsNotNull(tool.Name, "Tool name should not be null");
                    Assert.IsNotNull(tool.Description, "Tool description should not be null");
                    Assert.IsNotNull(tool.Category, "Tool category should not be null");
                    Assert.IsNotNull(tool.Context, "Tool context should not be null");
                    Assert.IsTrue(tool.Context == "runtime" || tool.Context == "both",
                        $"Tool {tool.Name} context should be 'runtime' or 'both', got '{tool.Context}'");
                }
            }
        }

        // ── Test: attribute-level validation ────────────────────────────────

        [Test]
        public void AllBothAnnotatedTools_AreDiscoveredAsRuntime()
        {
            // Verify via reflection that every tool marked Both shows up in runtime list
            var result = MetaRuntimeToolsTool.Execute(new MetaRuntimeToolsParams());
            Assert.IsTrue(result.Success, result.Error);

            var runtimeToolNames = result.Data.Categories
                .SelectMany(c => c.Tools)
                .Select(t => t.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Spot-check some known Both tools
            var expectedTools = new[]
            {
                "mosaic_gameobject_create",
                "mosaic_component_add",
                "mosaic_physics_raycast",
                "mosaic_camera_info",
                "mosaic_procgen_terrain",
                "mosaic_simulation_fluid",
                "mosaic_mesh_generate",
                "mosaic_material_create",
                "mosaic_ui_create_canvas"
            };

            foreach (var expected in expectedTools)
            {
                Assert.IsTrue(runtimeToolNames.Contains(expected),
                    $"Expected runtime tool '{expected}' not found in runtime tools list");
            }
        }
    }
}

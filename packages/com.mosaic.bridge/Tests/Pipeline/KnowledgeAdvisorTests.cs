using System;
using System.Collections.Generic;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Interfaces;
using Mosaic.Bridge.Core.Discovery;
using Mosaic.Bridge.Core.Pipeline;
using Mosaic.Bridge.Core.Pipeline.Stages;
using Mosaic.Bridge.Core.Server;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Mosaic.Bridge.Tests.Pipeline
{
    [TestFixture]
    public class KnowledgeAdvisorTests
    {
        private StubLogger _logger;
        private KnowledgeAdvisorStage _stage;

        [SetUp]
        public void SetUp()
        {
            _logger = new StubLogger();
            _stage = new KnowledgeAdvisorStage(_logger);
            // Reset the static accessor before each test
            KnowledgeProviderAccessor.Current = null;
        }

        [TearDown]
        public void TearDown()
        {
            KnowledgeProviderAccessor.Current = null;
        }

        // ── Material tool with known material adds suggestion ─────────────

        [Test]
        public void Execute_MaterialTool_WithKnownMaterial_AddsSuggestion()
        {
            var provider = new StubKnowledgeProvider();
            provider.Materials["wood_oak"] = new MaterialInfo
            {
                Name = "wood_oak",
                Roughness = 0.75f,
                Metalness = 0.0f,
                Albedo = new[] { 0.55f, 0.35f, 0.18f },
                Source = "measured"
            };
            KnowledgeProviderAccessor.Current = provider;

            var context = MakeContext(
                "mosaic_material_create",
                "material",
                new JObject { ["name"] = "wood_oak" },
                ExecutionMode.Validated);

            HandlerResponse response = null;
            var result = _stage.Execute(context, ref response);

            Assert.IsTrue(result, "Stage must never block the pipeline");
            Assert.IsNull(response, "Stage must not set a response");
            Assert.That(context.Warnings, Has.Count.EqualTo(1));
            Assert.That(context.Warnings[0], Does.Contain("wood_oak"));
            Assert.That(context.Warnings[0], Does.Contain("roughness=0.75"));
            Assert.That(context.Warnings[0], Does.Contain("metalness=0.00"));
            Assert.That(context.Warnings[0], Does.Contain("albedo=(0.55, 0.35, 0.18)"));
            Assert.That(context.Warnings[0], Does.Contain("[source: measured]"));
        }

        // ── Non-material tool gets no suggestions ─────────────────────────

        [Test]
        public void Execute_NonMaterialTool_NoSuggestions()
        {
            var provider = new StubKnowledgeProvider();
            provider.Materials["wood_oak"] = new MaterialInfo
            {
                Name = "wood_oak",
                Roughness = 0.75f,
                Metalness = 0.0f
            };
            KnowledgeProviderAccessor.Current = provider;

            var context = MakeContext(
                "mosaic_gameobject_create",
                "gameobject",
                new JObject { ["name"] = "wood_oak" },
                ExecutionMode.Validated);

            HandlerResponse response = null;
            var result = _stage.Execute(context, ref response);

            Assert.IsTrue(result);
            Assert.That(context.Warnings, Is.Empty,
                "Gameobject tools should not receive material advice");
            Assert.That(context.KBReferences, Is.Empty);
        }

        // ── Direct mode skips entirely ────────────────────────────────────

        [Test]
        public void Execute_DirectMode_Skips()
        {
            var provider = new StubKnowledgeProvider();
            provider.Materials["steel"] = new MaterialInfo
            {
                Name = "steel",
                Roughness = 0.3f,
                Metalness = 1.0f
            };
            KnowledgeProviderAccessor.Current = provider;

            var context = MakeContext(
                "mosaic_material_create",
                "material",
                new JObject { ["name"] = "steel" },
                ExecutionMode.Direct);

            HandlerResponse response = null;
            var result = _stage.Execute(context, ref response);

            Assert.IsTrue(result);
            Assert.That(context.Warnings, Is.Empty,
                "Direct mode should skip all knowledge advice");
            Assert.That(context.KBReferences, Is.Empty);
        }

        // ── Unknown material produces no warnings ─────────────────────────

        [Test]
        public void Execute_UnknownMaterial_NoSuggestions()
        {
            // Provider has no materials
            KnowledgeProviderAccessor.Current = new StubKnowledgeProvider();

            var context = MakeContext(
                "mosaic_material_create",
                "material",
                new JObject { ["name"] = "alien_goo" },
                ExecutionMode.Validated);

            HandlerResponse response = null;
            var result = _stage.Execute(context, ref response);

            Assert.IsTrue(result);
            Assert.That(context.Warnings, Is.Empty,
                "Unknown material should produce no warnings");
            Assert.That(context.KBReferences, Is.Empty);
        }

        // ── KB references are populated ───────────────────────────────────

        [Test]
        public void Execute_AddsKBReferences()
        {
            var provider = new StubKnowledgeProvider();
            provider.Materials["concrete"] = new MaterialInfo
            {
                Name = "concrete",
                Roughness = 0.9f,
                Metalness = 0.0f
            };
            KnowledgeProviderAccessor.Current = provider;

            var context = MakeContext(
                "mosaic_material_set-property",
                "material",
                new JObject { ["materialName"] = "concrete" },
                ExecutionMode.Verified);

            HandlerResponse response = null;
            _stage.Execute(context, ref response);

            Assert.That(context.KBReferences, Has.Count.EqualTo(1));
            Assert.That(context.KBReferences[0], Is.EqualTo("pbr:concrete"));
        }

        // ── Component tool with mass and material hint ────────────────────

        [Test]
        public void Execute_ComponentSetMass_WithMaterialHint_AddsSuggestion()
        {
            var provider = new StubKnowledgeProvider();
            provider.Materials["iron"] = new MaterialInfo
            {
                Name = "iron",
                Roughness = 0.4f,
                Metalness = 1.0f,
                DensityKgPerM3 = 7874f
            };
            KnowledgeProviderAccessor.Current = provider;

            var context = MakeContext(
                "mosaic_component_set_property",
                "component",
                new JObject
                {
                    ["propertyName"] = "mass",
                    ["value"] = 10,
                    ["materialHint"] = "iron"
                },
                ExecutionMode.Validated);

            HandlerResponse response = null;
            var result = _stage.Execute(context, ref response);

            Assert.IsTrue(result);
            Assert.That(context.Warnings, Has.Count.EqualTo(1));
            Assert.That(context.Warnings[0], Does.Contain("iron"));
            Assert.That(context.Warnings[0], Does.Contain("7874.0"));
            Assert.That(context.KBReferences, Has.Count.EqualTo(1));
            Assert.That(context.KBReferences[0], Is.EqualTo("physics:iron"));
        }

        // ── Null KB provider does not crash ───────────────────────────────

        [Test]
        public void Execute_NullKBProvider_NoCrash()
        {
            // KnowledgeProviderAccessor.Current is null (default from SetUp)
            var context = MakeContext(
                "mosaic_material_create",
                "material",
                new JObject { ["name"] = "wood_oak" },
                ExecutionMode.Validated);

            HandlerResponse response = null;
            var result = _stage.Execute(context, ref response);

            Assert.IsTrue(result, "Null KB provider must not crash the pipeline");
            Assert.That(context.Warnings, Is.Empty);
        }

        // ── Null parameters do not crash ──────────────────────────────────

        [Test]
        public void Execute_NullParameters_NoCrash()
        {
            KnowledgeProviderAccessor.Current = new StubKnowledgeProvider();

            var context = MakeContext(
                "mosaic_material_create",
                "material",
                null,
                ExecutionMode.Validated);

            HandlerResponse response = null;
            var result = _stage.Execute(context, ref response);

            Assert.IsTrue(result, "Null parameters must not crash");
            Assert.That(context.Warnings, Is.Empty);
        }

        // ── Reviewed mode also triggers advice ────────────────────────────

        [Test]
        public void Execute_ReviewedMode_AddsSuggestion()
        {
            var provider = new StubKnowledgeProvider();
            provider.Materials["glass"] = new MaterialInfo
            {
                Name = "glass",
                Roughness = 0.05f,
                Metalness = 0.0f,
                Ior = 1.52f
            };
            KnowledgeProviderAccessor.Current = provider;

            var context = MakeContext(
                "mosaic_material_create",
                "material",
                new JObject { ["name"] = "glass" },
                ExecutionMode.Reviewed);

            HandlerResponse response = null;
            _stage.Execute(context, ref response);

            Assert.That(context.Warnings, Has.Count.EqualTo(1));
            Assert.That(context.Warnings[0], Does.Contain("glass"));
        }

        // ── Category resolved from tool name when no ToolEntry ────────────

        [Test]
        public void Execute_CategoryFromToolName_WhenNoEntry()
        {
            var provider = new StubKnowledgeProvider();
            provider.Materials["copper"] = new MaterialInfo
            {
                Name = "copper",
                Roughness = 0.25f,
                Metalness = 1.0f,
                Source = "CIE"
            };
            KnowledgeProviderAccessor.Current = provider;

            var context = new ExecutionContext
            {
                ToolName = "mosaic_material_create",
                ToolEntry = null,
                Parameters = new JObject { ["name"] = "copper" },
                Mode = ExecutionMode.Validated
            };

            HandlerResponse response = null;
            _stage.Execute(context, ref response);

            Assert.That(context.Warnings, Has.Count.EqualTo(1));
            Assert.That(context.Warnings[0], Does.Contain("copper"));
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private static ExecutionContext MakeContext(
            string toolName, string category, JObject parameters, ExecutionMode mode)
        {
            return new ExecutionContext
            {
                ToolName = toolName,
                ToolEntry = category != null ? MakeEntry(toolName, category) : null,
                Parameters = parameters,
                Mode = mode
            };
        }

        private static ToolRegistryEntry MakeEntry(string toolName, string category)
        {
            var attr = new MosaicToolAttribute(
                $"{category}/test",
                "Test tool",
                isReadOnly: false,
                category: category);
            return new ToolRegistryEntry(toolName, attr, null, null);
        }

        // ── Test doubles ───────────────────────────────────────────────────

        private class StubLogger : IMosaicLogger
        {
            public LogLevel MinimumLevel { get; set; } = LogLevel.Trace;

            public void Trace(string message, params (string Key, object Value)[] context) { }
            public void Debug(string message, params (string Key, object Value)[] context) { }
            public void Info(string message, params (string Key, object Value)[] context) { }
            public void Warn(string message, params (string Key, object Value)[] context) { }
            public void Error(string message, Exception exception = null,
                params (string Key, object Value)[] context) { }
            public bool IsEnabled(LogLevel level) => level >= MinimumLevel;
        }

        private class StubKnowledgeProvider : IKnowledgeProvider
        {
            public Dictionary<string, MaterialInfo> Materials { get; }
                = new Dictionary<string, MaterialInfo>(StringComparer.OrdinalIgnoreCase);

            public double? GetConstant(string key) => null;

            public MaterialInfo GetMaterial(string materialName)
            {
                return Materials.TryGetValue(materialName, out var info) ? info : null;
            }

            public IReadOnlyList<MaterialInfo> FindMaterials(Func<MaterialInfo, bool> filter)
            {
                var results = new List<MaterialInfo>();
                foreach (var m in Materials.Values)
                {
                    if (filter(m)) results.Add(m);
                }
                return results;
            }

            public FormulaInfo GetFormula(string formulaName) => null;

            public string GetContextForCategory(string category) => string.Empty;

            public bool HasEntry(string entryId) => Materials.ContainsKey(entryId);
        }
    }
}

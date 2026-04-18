using System.Collections.Generic;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Core.Discovery;
using Mosaic.Bridge.Core.Pipeline;
using Mosaic.Bridge.Core.Pipeline.Stages;
using Mosaic.Bridge.Core.Pipeline.Validation;
using Mosaic.Bridge.Core.Server;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Mosaic.Bridge.Tests.Pipeline
{
    [TestFixture]
    public class SemanticValidatorTests
    {
        // ── TransformRangeRule ──────────────────────────────────────────────

        [Test]
        public void TransformRange_ExtremePosition_ReturnsWarning()
        {
            var rule = new TransformRangeRule();
            var context = MakeContext("mosaic_gameobject_create", "gameobject",
                new JObject { ["position"] = new JObject { ["x"] = 0, ["y"] = -1000, ["z"] = 0 } });

            var result = rule.Validate(context);

            Assert.IsTrue(result.IsValid, "Should not reject (warning only)");
            Assert.AreEqual(ValidationSeverity.Warning, result.Severity);
            Assert.That(result.Message, Does.Contain("-1000"));
            Assert.That(result.Message, Does.Contain("outside typical range"));
        }

        [Test]
        public void TransformRange_BeyondPrecision_Rejects()
        {
            var rule = new TransformRangeRule();
            var context = MakeContext("mosaic_gameobject_create", "gameobject",
                new JObject { ["position"] = new JObject { ["x"] = 200000, ["y"] = 0, ["z"] = 0 } });

            var result = rule.Validate(context);

            Assert.IsFalse(result.IsValid, "Should reject extreme position");
            Assert.AreEqual(ValidationSeverity.Error, result.Severity);
            Assert.That(result.Message, Does.Contain("precision"));
        }

        [Test]
        public void TransformRange_NormalPosition_Passes()
        {
            var rule = new TransformRangeRule();
            var context = MakeContext("mosaic_gameobject_create", "gameobject",
                new JObject { ["position"] = new JObject { ["x"] = 10, ["y"] = 5, ["z"] = -3 } });

            var result = rule.Validate(context);

            Assert.IsTrue(result.IsValid);
            Assert.IsNull(result.Message);
        }

        [Test]
        public void TransformRange_NoPosition_Passes()
        {
            var rule = new TransformRangeRule();
            var context = MakeContext("mosaic_gameobject_create", "gameobject",
                new JObject { ["name"] = "Cube" });

            var result = rule.Validate(context);

            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void TransformRange_NegativePrecisionLimit_Rejects()
        {
            var rule = new TransformRangeRule();
            var context = MakeContext("mosaic_gameobject_set_transform", "gameobject",
                new JObject { ["position"] = new JObject { ["x"] = 0, ["y"] = 0, ["z"] = -150000 } });

            var result = rule.Validate(context);

            Assert.IsFalse(result.IsValid, "Negative values beyond precision range should also reject");
        }

        // ── PbrRangeRule ───────────────────────────────────────────────────

        [Test]
        public void PbrRange_RoughnessOver1_Rejects()
        {
            var rule = new PbrRangeRule();
            var context = MakeContext("mosaic_material_set-property", "material",
                new JObject { ["roughness"] = 5.0 });

            var result = rule.Validate(context);

            Assert.IsFalse(result.IsValid, "Roughness > 1 should be rejected");
            Assert.AreEqual(ValidationSeverity.Error, result.Severity);
            Assert.That(result.Message, Does.Contain("roughness"));
        }

        [Test]
        public void PbrRange_ValidValues_Passes()
        {
            var rule = new PbrRangeRule();
            var context = MakeContext("mosaic_material_set-property", "material",
                new JObject { ["roughness"] = 0.5, ["metallic"] = 0.8 });

            var result = rule.Validate(context);

            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void PbrRange_NegativeValue_Rejects()
        {
            var rule = new PbrRangeRule();
            var context = MakeContext("mosaic_material_create", "material",
                new JObject { ["metallic"] = -0.1 });

            var result = rule.Validate(context);

            Assert.IsFalse(result.IsValid, "Negative PBR values should be rejected");
        }

        [Test]
        public void PbrRange_BoundaryValues_Pass()
        {
            var rule = new PbrRangeRule();
            var context = MakeContext("mosaic_material_set-property", "material",
                new JObject { ["roughness"] = 0.0, ["metallic"] = 1.0 });

            var result = rule.Validate(context);

            Assert.IsTrue(result.IsValid, "Boundary values 0 and 1 should pass");
        }

        [Test]
        public void PbrRange_NestedProperties_Rejects()
        {
            var rule = new PbrRangeRule();
            var context = MakeContext("mosaic_material_set-property", "material",
                new JObject { ["properties"] = new JObject { ["smoothness"] = 2.0 } });

            var result = rule.Validate(context);

            Assert.IsFalse(result.IsValid, "Nested PBR properties should also be validated");
        }

        [Test]
        public void PbrRange_NonPbrProperty_Passes()
        {
            var rule = new PbrRangeRule();
            var context = MakeContext("mosaic_material_set-property", "material",
                new JObject { ["color"] = "#FF0000", ["intensity"] = 5.0 });

            var result = rule.Validate(context);

            Assert.IsTrue(result.IsValid, "Non-PBR properties should be ignored");
        }

        // ── ScriptExistsRule (basic checks without file system) ────────────

        [Test]
        public void ScriptExists_NonCreateTool_Passes()
        {
            var rule = new ScriptExistsRule();
            var context = MakeContext("mosaic_script_read", "script",
                new JObject { ["path"] = "Assets/Scripts/Foo.cs" });

            var result = rule.Validate(context);

            Assert.IsTrue(result.IsValid, "Read operations should not trigger existence check");
        }

        [Test]
        public void ScriptExists_OverwriteTrue_Passes()
        {
            var rule = new ScriptExistsRule();
            var context = MakeContext("mosaic_script_create", "script",
                new JObject { ["path"] = "Assets/Scripts/Foo.cs", ["overwrite"] = true });

            var result = rule.Validate(context);

            Assert.IsTrue(result.IsValid, "overwrite=true should bypass existence check");
        }

        [Test]
        public void ScriptExists_NoPath_Passes()
        {
            var rule = new ScriptExistsRule();
            var context = MakeContext("mosaic_script_create", "script",
                new JObject { ["className"] = "Foo" });

            var result = rule.Validate(context);

            Assert.IsTrue(result.IsValid, "Missing path should pass (handler will report the error)");
        }

        // ── SemanticValidatorStage integration ─────────────────────────────

        [Test]
        public void SemanticValidatorStage_Warning_ContinuesPipeline()
        {
            var rules = new List<IValidationRule> { new TransformRangeRule() };
            var stage = new SemanticValidatorStage(rules);

            var context = MakeContext("mosaic_gameobject_create", "gameobject",
                new JObject { ["position"] = new JObject { ["x"] = 0, ["y"] = -1000, ["z"] = 0 } });

            HandlerResponse response = null;
            var continueExecution = stage.Execute(context, ref response);

            Assert.IsTrue(continueExecution, "Warning should NOT abort the pipeline");
            Assert.IsNull(response, "Response should remain null for warnings");
            Assert.That(context.Warnings, Has.Count.GreaterThan(0), "Warning should be added to context");
            Assert.That(context.Warnings[0], Does.Contain("-1000"));
        }

        [Test]
        public void SemanticValidatorStage_Error_AbortsPipeline()
        {
            var rules = new List<IValidationRule> { new TransformRangeRule() };
            var stage = new SemanticValidatorStage(rules);

            var context = MakeContext("mosaic_gameobject_create", "gameobject",
                new JObject { ["position"] = new JObject { ["x"] = 200000, ["y"] = 0, ["z"] = 0 } });

            HandlerResponse response = null;
            var continueExecution = stage.Execute(context, ref response);

            Assert.IsFalse(continueExecution, "Error should abort the pipeline");
            Assert.IsNotNull(response, "Response must be set on abort");
            Assert.AreEqual(400, response.StatusCode);
            Assert.That(response.Body, Does.Contain("VALIDATION_ERROR"));
            Assert.That(response.Body, Does.Contain("precision"));
        }

        [Test]
        public void SemanticValidatorStage_MismatchedCategory_SkipsRule()
        {
            var rules = new List<IValidationRule> { new PbrRangeRule() };
            var stage = new SemanticValidatorStage(rules);

            // PbrRangeRule has category "material" — should not run for gameobject tools
            var context = MakeContext("mosaic_gameobject_create", "gameobject",
                new JObject { ["roughness"] = 5.0 });

            HandlerResponse response = null;
            var continueExecution = stage.Execute(context, ref response);

            Assert.IsTrue(continueExecution, "Rule with mismatched category should be skipped");
        }

        [Test]
        public void SemanticValidatorStage_CategoryFromToolName_WhenNoEntry()
        {
            // No ToolEntry — category parsed from tool name "mosaic_material_set-property"
            var rules = new List<IValidationRule> { new PbrRangeRule() };
            var stage = new SemanticValidatorStage(rules);

            var context = new ExecutionContext
            {
                ToolName = "mosaic_material_set-property",
                ToolEntry = null,
                Parameters = new JObject { ["roughness"] = 5.0 }
            };

            HandlerResponse response = null;
            var continueExecution = stage.Execute(context, ref response);

            Assert.IsFalse(continueExecution, "Should still match by parsing category from tool name");
            Assert.AreEqual(400, response.StatusCode);
        }

        [Test]
        public void SemanticValidatorStage_MultipleRules_StopsOnFirstError()
        {
            var rules = new List<IValidationRule>
            {
                new AlwaysRejectRule("First failure"),
                new AlwaysRejectRule("Second failure")
            };
            var stage = new SemanticValidatorStage(rules);

            var context = MakeContext("mosaic_gameobject_create", null,
                new JObject());

            HandlerResponse response = null;
            stage.Execute(context, ref response);

            Assert.That(response.Body, Does.Contain("First failure"));
            Assert.That(response.Body, Does.Not.Contain("Second failure"));
        }

        [Test]
        public void SemanticValidatorStage_RuleThrowsException_TreatedAsWarning()
        {
            var rules = new List<IValidationRule> { new ThrowingRule() };
            var stage = new SemanticValidatorStage(rules);

            var context = MakeContext("mosaic_gameobject_create", null,
                new JObject());

            HandlerResponse response = null;
            var continueExecution = stage.Execute(context, ref response);

            Assert.IsTrue(continueExecution, "Exception in rule should not abort pipeline");
            Assert.That(context.Warnings, Has.Count.GreaterThan(0));
            Assert.That(context.Warnings[0], Does.Contain("ThrowingRule"));
        }

        // ── Helpers ────────────────────────────────────────────────────────

        private static ExecutionContext MakeContext(string toolName, string category, JObject parameters)
        {
            return new ExecutionContext
            {
                ToolName = toolName,
                ToolEntry = category != null ? MakeEntry(toolName, category) : null,
                Parameters = parameters
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

        private class AlwaysRejectRule : IValidationRule
        {
            private readonly string _message;
            public AlwaysRejectRule(string message) => _message = message;
            public string Category => null; // applies to all
            public ValidationResult Validate(ExecutionContext context) => ValidationResult.Reject(_message);
        }

        private class ThrowingRule : IValidationRule
        {
            public string Category => null;
            public ValidationResult Validate(ExecutionContext context) =>
                throw new System.InvalidOperationException("Intentional test exception");
        }
    }
}

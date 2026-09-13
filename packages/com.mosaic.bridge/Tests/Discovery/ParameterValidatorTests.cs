using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Core.Discovery;
using NUnit.Framework;

namespace Mosaic.Bridge.Tests.Discovery
{
    [TestFixture]
    public class ParameterValidatorTests
    {
        // ── Test fixture param types ─────────────────────────────────────────────

        private class SimpleParams
        {
            public string Name { get; set; }
            [RequiredAttribute] public string RequiredField { get; set; }
            public int Count { get; set; }
        }

        private class NoRequiredParams
        {
            public string Value { get; set; }
        }

        // ── Tests ────────────────────────────────────────────────────────────────

        [Test]
        public void Bind_ValidJson_ReturnsSuccess()
        {
            var result = ParameterValidator.Bind<SimpleParams>("{\"RequiredField\":\"hello\",\"Name\":\"world\"}");

            Assert.IsTrue(result.IsValid);
            Assert.IsNotNull(result.Value);
            var p = (SimpleParams)result.Value;
            Assert.AreEqual("hello", p.RequiredField);
            Assert.AreEqual("world", p.Name);
        }

        [Test]
        public void Bind_MissingRequiredField_ReturnsFail()
        {
            var result = ParameterValidator.Bind<SimpleParams>("{\"Name\":\"world\"}");

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(ErrorCodes.INVALID_PARAM, result.ErrorCode);
            StringAssert.Contains("RequiredField", result.ErrorMessage);
        }

        [Test]
        public void Bind_EmptyStringRequiredField_ReturnsFail()
        {
            var result = ParameterValidator.Bind<SimpleParams>("{\"RequiredField\":\" \"}");

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(ErrorCodes.INVALID_PARAM, result.ErrorCode);
        }

        [Test]
        public void Bind_InvalidJson_ReturnsFail()
        {
            var result = ParameterValidator.Bind<SimpleParams>("not valid json{{{");

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(ErrorCodes.INVALID_PARAM, result.ErrorCode);
        }

        [Test]
        public void Bind_NullJson_ReturnsFail()
        {
            var result = ParameterValidator.Bind<SimpleParams>(null);

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(ErrorCodes.INVALID_PARAM, result.ErrorCode);
        }

        [Test]
        public void Bind_NullTargetType_ReturnsOkNull()
        {
            var result = ParameterValidator.Bind("{\"anything\":\"ignored\"}", null);

            Assert.IsTrue(result.IsValid);
            Assert.IsNull(result.Value);
        }

        [Test]
        public void Bind_TypeMismatch_ReturnsFail()
        {
            // "not-a-number" cannot be deserialized into int Count
            var result = ParameterValidator.Bind<SimpleParams>("{\"RequiredField\":\"x\",\"Count\":\"not-a-number\"}");

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(ErrorCodes.INVALID_PARAM, result.ErrorCode);
        }

        [Test]
        public void Bind_Generic_Convenience()
        {
            var result = ParameterValidator.Bind<NoRequiredParams>("{\"Value\":\"test\"}");

            Assert.IsTrue(result.IsValid);
            Assert.IsNotNull(result.Value);
            var p = (NoRequiredParams)result.Value;
            Assert.AreEqual("test", p.Value);
        }

        // ── Unknown parameters ───────────────────────────────────────────────────
        // The generated schema promises `additionalProperties: false`, and the binder ignored
        // anything extra. A caller that misspelled a parameter, or invented one, got silence and a
        // successful-looking result computed from defaults — the parameter simply had no effect.
        //
        // Found the hard way: scene/create-object was called with a `primitiveType` it does not
        // accept. The call succeeded, the parameter was dropped, and the result was read as though
        // it had been honoured. That misdiagnosis then invalidated a downstream experiment.

        [Test]
        public void Bind_UnknownParameter_IsRejected()
        {
            var result = ParameterValidator.Bind<SimpleParams>(
                "{\"RequiredField\":\"hello\",\"primitiveType\":\"Cube\"}");
            Assert.IsFalse(result.IsValid, "an unknown parameter must not be silently ignored");
            Assert.AreEqual(ErrorCodes.INVALID_PARAM, result.ErrorCode);
        }

        [Test]
        public void Bind_UnknownParameter_NamesItAndTheValidOnes()
        {
            // "Could not find member 'x'" says what is wrong but not what to write instead, and the
            // caller is here precisely because they believed a name that does not exist.
            var result = ParameterValidator.Bind<SimpleParams>(
                "{\"RequiredField\":\"hello\",\"primitiveType\":\"Cube\"}");
            StringAssert.Contains("primitiveType", result.ErrorMessage);
            StringAssert.Contains("name", result.ErrorMessage);
            StringAssert.Contains("count", result.ErrorMessage);
            StringAssert.Contains("requiredField (required)", result.ErrorMessage);
        }

        [Test]
        public void Bind_KnownParametersOnly_StillSucceeds()
        {
            // The narrowing must not cost the ordinary case.
            var result = ParameterValidator.Bind<SimpleParams>(
                "{\"RequiredField\":\"hello\",\"Name\":\"world\",\"Count\":3}");
            Assert.IsTrue(result.IsValid, result.ErrorMessage);
        }

        [Test]
        public void Bind_MissingOptionalParameters_StillSucceeds()
        {
            var result = ParameterValidator.Bind<SimpleParams>("{\"RequiredField\":\"hello\"}");
            Assert.IsTrue(result.IsValid, result.ErrorMessage);
        }

        // ── Nested array-of-objects (H-1) ───────────────────────────────────────
        // Found on meta/batch/execute (`calls:[{stopOnError:true}]`) and
        // animation/transition (`conditions:[{hasExitTime:false}]`): an unknown key on an item
        // inside an array-of-objects parameter was validated against the PARENT route's own
        // property names instead of the item's own schema, so the error rejected a key while
        // its own "accepts" list named it. These fixtures mirror that exact shape.

        private class BatchCallFixture
        {
            [RequiredAttribute] public string ToolName { get; set; }
            public Newtonsoft.Json.Linq.JObject Arguments { get; set; }
        }

        private class BatchExecuteParamsFixture
        {
            [RequiredAttribute] public System.Collections.Generic.List<BatchCallFixture> Calls { get; set; }
            public bool StopOnError { get; set; }
        }

        private class ConditionFixture
        {
            public string ParameterName { get; set; }
            public string Mode { get; set; }
        }

        private class TransitionParamsFixture
        {
            [RequiredAttribute] public string Action { get; set; }
            public ConditionFixture[] Conditions { get; set; }
        }

        [Test]
        public void Bind_UnknownKeyInsideArrayOfObjects_NamesTheItemsOwnAcceptedKeys_NotTheParents()
        {
            // "stopOnError" is a real key — but on the PARENT (BatchExecuteParamsFixture), not on
            // the array item (BatchCallFixture) where it was actually written.
            var result = ParameterValidator.Bind<BatchExecuteParamsFixture>(
                "{\"Calls\":[{\"ToolName\":\"x\",\"stopOnError\":true}]}");

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("stopOnError", result.ErrorMessage);
            // Must list the ITEM type's own keys (toolName, arguments) — not the parent's
            // (calls, stopOnError), which is what a scope-confused validator would report, and
            // which is self-contradictory: it would reject 'stopOnError' while listing it as
            // accepted.
            StringAssert.Contains("toolName (required)", result.ErrorMessage);
            StringAssert.Contains("arguments", result.ErrorMessage);
            Assert.IsFalse(result.ErrorMessage.Contains("calls,"),
                "must not fall back to the parent's own property list");
        }

        [Test]
        public void Bind_UnknownKeyOnArrayOfObjectsUsingArrayType_NamesTheItemsOwnAcceptedKeys()
        {
            // Same shape as animation/transition's `conditions:[{hasExitTime:false}]`, but with
            // a real C# array (T[]) instead of List<T> — the other common collection shape.
            var result = ParameterValidator.Bind<TransitionParamsFixture>(
                "{\"Action\":\"add\",\"Conditions\":[{\"hasExitTime\":false}]}");

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("hasExitTime", result.ErrorMessage);
            StringAssert.Contains("parameterName", result.ErrorMessage);
            StringAssert.Contains("mode", result.ErrorMessage);
        }

        [Test]
        public void Bind_UnknownTopLevelParameter_StillNamesTheTopLevelAcceptedKeys()
        {
            // The narrowing to the item's own schema must not regress the ordinary, non-nested
            // case: an unknown key on the route's own top-level object still lists the route's
            // own accepted keys.
            var result = ParameterValidator.Bind<BatchExecuteParamsFixture>(
                "{\"Calls\":[],\"bogus\":1}");

            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("bogus", result.ErrorMessage);
            StringAssert.Contains("calls (required)", result.ErrorMessage);
            StringAssert.Contains("stopOnError", result.ErrorMessage);
        }
    }
}

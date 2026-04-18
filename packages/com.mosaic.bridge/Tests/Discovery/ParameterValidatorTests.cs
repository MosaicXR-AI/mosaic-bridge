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
    }
}

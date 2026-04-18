using NUnit.Framework;
using Mosaic.Bridge.Tools.Packages;

namespace Mosaic.Bridge.Tests.Packages
{
    [TestFixture]
    [Category("Packages")]
    public class PackageAddTests
    {
        [Test]
        public void Add_NullIdentifier_ReturnsInvalidParam()
        {
            var result = PackageAddTool.Execute(new PackageAddParams { Identifier = null });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Add_EmptyIdentifier_ReturnsInvalidParam()
        {
            var result = PackageAddTool.Execute(new PackageAddParams { Identifier = "" });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Add_WhitespaceIdentifier_ReturnsInvalidParam()
        {
            var result = PackageAddTool.Execute(new PackageAddParams { Identifier = "   " });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Params_IdentifierProperty_RoundTrips()
        {
            var p = new PackageAddParams { Identifier = "com.unity.test@1.0.0" };
            Assert.AreEqual("com.unity.test@1.0.0", p.Identifier);
        }
    }
}

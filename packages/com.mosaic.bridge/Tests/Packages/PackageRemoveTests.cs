using NUnit.Framework;
using Mosaic.Bridge.Tools.Packages;

namespace Mosaic.Bridge.Tests.Packages
{
    [TestFixture]
    [Category("Packages")]
    public class PackageRemoveTests
    {
        [Test]
        public void Remove_NullName_ReturnsInvalidParam()
        {
            var result = PackageRemoveTool.Execute(new PackageRemoveParams { Name = null });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Remove_EmptyName_ReturnsInvalidParam()
        {
            var result = PackageRemoveTool.Execute(new PackageRemoveParams { Name = "" });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Remove_WhitespaceName_ReturnsInvalidParam()
        {
            var result = PackageRemoveTool.Execute(new PackageRemoveParams { Name = "   " });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Params_NameProperty_RoundTrips()
        {
            var p = new PackageRemoveParams { Name = "com.unity.cinemachine" };
            Assert.AreEqual("com.unity.cinemachine", p.Name);
        }
    }
}

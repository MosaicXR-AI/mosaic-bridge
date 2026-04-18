using NUnit.Framework;
using Mosaic.Bridge.Tools.Packages;

namespace Mosaic.Bridge.Tests.Packages
{
    [TestFixture]
    [Category("Packages")]
    public class PackageSearchTests
    {
        [Test]
        public void Search_NullQuery_ReturnsInvalidParam()
        {
            var result = PackageSearchTool.Execute(new PackageSearchParams { Query = null });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Search_EmptyQuery_ReturnsInvalidParam()
        {
            var result = PackageSearchTool.Execute(new PackageSearchParams { Query = "" });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Search_WhitespaceQuery_ReturnsInvalidParam()
        {
            var result = PackageSearchTool.Execute(new PackageSearchParams { Query = "   " });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Params_QueryProperty_RoundTrips()
        {
            var p = new PackageSearchParams { Query = "cinemachine" };
            Assert.AreEqual("cinemachine", p.Query);
        }
    }
}

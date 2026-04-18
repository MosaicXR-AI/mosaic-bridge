using System.Linq;
using NUnit.Framework;
using Mosaic.Bridge.Tools.Packages;

namespace Mosaic.Bridge.Tests.Packages
{
    [TestFixture]
    [Category("Integration")]
    [Category("Packages")]
    public class PackageListTests
    {
        [Test]
        public void List_DefaultParams_ReturnsBridgePackage()
        {
            // Use offline mode to avoid 30s timeout when registry is unreachable
            var result = PackageListTool.Execute(new PackageListParams { OfflineMode = true });

            Assert.IsTrue(result.Success, $"PackageList failed: {result.Error}");
            Assert.IsNotNull(result.Data);
            Assert.IsNotNull(result.Data.Packages);
            Assert.Greater(result.Data.Count, 0, "Expected at least one installed package");

            // The bridge package itself should always be installed
            var bridge = result.Data.Packages
                .FirstOrDefault(p => p.Name == "com.mosaic.bridge");
            Assert.IsNotNull(bridge,
                "Expected com.mosaic.bridge to appear in the installed packages list");
            Assert.IsFalse(string.IsNullOrEmpty(bridge.Version));
            Assert.IsFalse(string.IsNullOrEmpty(bridge.DisplayName));
        }

        [Test]
        public void List_OfflineMode_ReturnsPackages()
        {
            var result = PackageListTool.Execute(new PackageListParams { OfflineMode = true });

            Assert.IsTrue(result.Success, $"PackageList (offline) failed: {result.Error}");
            Assert.IsNotNull(result.Data);
            Assert.Greater(result.Data.Count, 0, "Expected at least one package in offline mode");
        }

        [Test]
        public void List_ResultCountMatchesPackagesList()
        {
            var result = PackageListTool.Execute(new PackageListParams { OfflineMode = true });

            Assert.IsTrue(result.Success);
            Assert.AreEqual(result.Data.Packages.Count, result.Data.Count,
                "Count should match the number of packages in the list");
        }

        [Test]
        public void List_AllPackagesHaveNameAndVersion()
        {
            var result = PackageListTool.Execute(new PackageListParams { OfflineMode = true });

            Assert.IsTrue(result.Success);
            foreach (var pkg in result.Data.Packages)
            {
                Assert.IsFalse(string.IsNullOrEmpty(pkg.Name),
                    "Every package should have a name");
                Assert.IsFalse(string.IsNullOrEmpty(pkg.Version),
                    $"Package '{pkg.Name}' should have a version");
                Assert.IsFalse(string.IsNullOrEmpty(pkg.Source),
                    $"Package '{pkg.Name}' should have a source");
            }
        }
    }
}

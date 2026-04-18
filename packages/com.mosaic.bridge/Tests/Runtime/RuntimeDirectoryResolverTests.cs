using System.IO;
using Mosaic.Bridge.Core.Runtime;
using NUnit.Framework;

namespace Mosaic.Bridge.Tests.Runtime
{
    [TestFixture]
    public class RuntimeDirectoryResolverTests
    {
        [Test]
        public void Resolve_ReturnsNonNullNonEmptyAbsolutePath()
        {
            var path = RuntimeDirectoryResolver.Resolve();

            Assert.IsNotNull(path);
            Assert.IsNotEmpty(path);
            Assert.IsTrue(Path.IsPathRooted(path), $"Expected absolute path but got: {path}");
        }

        [Test]
        public void Resolve_CreatesDirectoryIfMissing()
        {
            var path = RuntimeDirectoryResolver.Resolve();

            Assert.IsTrue(Directory.Exists(path), $"Expected directory to exist at: {path}");
        }

        [Test]
        public void Resolve_CalledTwice_ReturnsSamePath()
        {
            var first = RuntimeDirectoryResolver.Resolve();
            var second = RuntimeDirectoryResolver.Resolve();

            Assert.AreEqual(first, second);
        }

        [Test]
        public void GetDiscoveryFilePath_EndsWithExpectedFilename()
        {
            var path = RuntimeDirectoryResolver.GetDiscoveryFilePath();

            Assert.IsTrue(path.EndsWith("bridge-discovery.json"),
                $"Expected path to end with 'bridge-discovery.json' but got: {path}");
        }

        [Test]
        public void GetLogDirectoryPath_CreatesLogsSubdirectory()
        {
            var path = RuntimeDirectoryResolver.GetLogDirectoryPath();

            Assert.IsTrue(Directory.Exists(path), $"Expected logs directory to exist at: {path}");
            Assert.IsTrue(path.EndsWith("logs") || path.EndsWith("logs/") || path.EndsWith("logs\\"),
                $"Expected path to end with 'logs' but got: {path}");
        }
    }
}

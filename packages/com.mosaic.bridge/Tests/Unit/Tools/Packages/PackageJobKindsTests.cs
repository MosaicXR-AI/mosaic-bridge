using NUnit.Framework;
using Mosaic.Bridge.Core.Jobs;
using Mosaic.Bridge.Tools.Packages;

namespace Mosaic.Bridge.Tests.Unit.Tools.Packages
{
    // L15: package/add and package/remove offer no cancellation in the underlying Unity
    // PackageManager API, so Cancel must always decline rather than pretend it stopped anything.
    // Start/Probe are not covered here — both call into Client.Add/Client.Remove/PackageInfo,
    // which need a live Editor to mean anything.
    [TestFixture]
    public class PackageJobKindsTests
    {
        [Test]
        public void PackageAddJobKind_Cancel_AlwaysDeclines()
        {
            IJobKind kind = new PackageAddJobKind();

            Assert.IsFalse(kind.Cancel(new JobRecord { JobId = "j1", Kind = "package/add" }));
        }

        [Test]
        public void PackageRemoveJobKind_Cancel_AlwaysDeclines()
        {
            IJobKind kind = new PackageRemoveJobKind();

            Assert.IsFalse(kind.Cancel(new JobRecord { JobId = "j2", Kind = "package/remove" }));
        }
    }
}

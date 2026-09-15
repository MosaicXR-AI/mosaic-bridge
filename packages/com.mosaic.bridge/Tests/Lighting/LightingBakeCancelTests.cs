using NUnit.Framework;
using UnityEditor;
using Mosaic.Bridge.Tools.Lighting;

namespace Mosaic.Bridge.Tests.Lighting
{
    // O4 §4.3: lighting/bake started a bake with no way to cancel it.
    [TestFixture]
    [Category("Lighting")]
    public class LightingBakeCancelTests
    {
        [Test]
        public void Cancel_NoBakeRunning_IsANoOpAndSucceeds()
        {
            Assert.IsFalse(Lightmapping.isRunning, "test precondition: no bake should be running");

            var result = LightingBakeCancelTool.Execute(new LightingBakeCancelParams());

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsFalse(result.Data.WasRunning);
            Assert.IsFalse(Lightmapping.isRunning);
        }
    }
}

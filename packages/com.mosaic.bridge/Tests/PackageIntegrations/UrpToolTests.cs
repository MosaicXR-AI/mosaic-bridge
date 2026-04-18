#if MOSAIC_HAS_URP
using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Tools.URP;

namespace Mosaic.Bridge.Tests.PackageIntegrations
{
    [TestFixture]
    [Category("PackageIntegration")]
    public class UrpToolTests
    {
        [TearDown]
        public void TearDown()
        {
            foreach (var name in new[] { "URP_Volume", "URP_Decal" })
            {
                var go = GameObject.Find(name);
                if (go != null) Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Volume_Create_ReturnsSuccess()
        {
            var result = UrpVolumeTool.Execute(new UrpVolumeParams
            {
                Name = "URP_Volume", IsGlobal = true
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(GameObject.Find("URP_Volume"));
        }

        [Test]
        public void Decal_Create_ReturnsSuccess()
        {
            var result = UrpDecalTool.Execute(new UrpDecalParams
            {
                Name = "URP_Decal",
                Size = new float[] { 2, 2, 2 },
                Position = new float[] { 0, 1, 0 }
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(GameObject.Find("URP_Decal"));
        }

        [Test]
        public void RendererFeature_List_ReturnsSuccess()
        {
            var result = UrpRendererFeatureTool.Execute(new UrpRendererFeatureParams
            {
                Action = "list"
            });
            Assert.IsTrue(result.Success, result.Error);
        }

        [Test]
        public void RendererFeature_InvalidAction_ReturnsFail()
        {
            var result = UrpRendererFeatureTool.Execute(new UrpRendererFeatureParams
            {
                Action = "invalid"
            });
            Assert.IsFalse(result.Success);
        }

        [Test]
        public void Volume_WithOverrides_ReturnsSuccess()
        {
            var result = UrpVolumeTool.Execute(new UrpVolumeParams
            {
                Name = "URP_Volume", IsGlobal = false, Priority = 5f
            });
            Assert.IsTrue(result.Success, result.Error);
        }
    }
}
#endif

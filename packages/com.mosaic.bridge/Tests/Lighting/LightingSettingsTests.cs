using NUnit.Framework;
using UnityEditor;
using Mosaic.Bridge.Tools.Lighting;

namespace Mosaic.Bridge.Tests.Lighting
{
    // O4 §4.3: without this, an M-size scene lighting config (lightmapper, resolution, mixed mode,
    // sample counts) had no route at all — a bake could not be tuned fast enough for a course capture.
    [TestFixture]
    [Category("Lighting")]
    public class LightingSettingsTests
    {
        private const string AssetPath = "Assets/MosaicTestLightingSettings.lighting";

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.LightingSettings>(AssetPath) != null)
                AssetDatabase.DeleteAsset(AssetPath);
        }

        [Test]
        public void Create_AppliesFieldsAndPersists()
        {
            var result = LightingSettingsTool.Execute(new LightingSettingsParams
            {
                Action = "create", AssetPath = AssetPath,
                Lightmapper = "ProgressiveGPU", LightmapResolution = 4f, LightmapMaxSize = 2048,
                Ao = true, MixedBakeMode = "Shadowmask", DirectSampleCount = 32, MaxBounces = 4,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("ProgressiveGPU", result.Data.Lightmapper);
            Assert.AreEqual(4f, result.Data.LightmapResolution, 0.0001f);
            Assert.AreEqual(2048, result.Data.LightmapMaxSize);
            Assert.IsTrue(result.Data.Ao);
            Assert.AreEqual("Shadowmask", result.Data.MixedBakeMode);
            Assert.AreEqual(32, result.Data.DirectSampleCount);
            Assert.AreEqual(4, result.Data.MaxBounces);

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.LightingSettings>(AssetPath);
            Assert.IsNotNull(asset);
            Assert.AreEqual(UnityEngine.LightingSettings.Lightmapper.ProgressiveGPU, asset.lightmapper);
        }

        [Test]
        public void Get_ReturnsCurrentValues()
        {
            LightingSettingsTool.Execute(new LightingSettingsParams
            {
                Action = "create", AssetPath = AssetPath, LightmapResolution = 8f,
            });

            var result = LightingSettingsTool.Execute(new LightingSettingsParams
            {
                Action = "get", AssetPath = AssetPath,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(8f, result.Data.LightmapResolution, 0.0001f);
        }

        [Test]
        public void Set_UpdatesExistingAsset()
        {
            LightingSettingsTool.Execute(new LightingSettingsParams
            {
                Action = "create", AssetPath = AssetPath, LightmapResolution = 8f,
            });

            var result = LightingSettingsTool.Execute(new LightingSettingsParams
            {
                Action = "set", AssetPath = AssetPath, LightmapResolution = 16f, RealtimeGI = false,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(16f, result.Data.LightmapResolution, 0.0001f);
            Assert.IsFalse(result.Data.RealtimeGI);

            var reloaded = AssetDatabase.LoadAssetAtPath<UnityEngine.LightingSettings>(AssetPath);
            Assert.AreEqual(16f, reloaded.lightmapResolution, 0.0001f);
        }

        [Test]
        public void Get_MissingAsset_ReturnsNotFound()
        {
            var result = LightingSettingsTool.Execute(new LightingSettingsParams
            {
                Action = "get", AssetPath = "Assets/NoSuchSettings.lighting",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void Create_UnknownLightmapper_ReturnsInvalidParam()
        {
            var result = LightingSettingsTool.Execute(new LightingSettingsParams
            {
                Action = "create", AssetPath = AssetPath, Lightmapper = "Bogus",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Execute_UnknownAction_ReturnsInvalidParam()
        {
            var result = LightingSettingsTool.Execute(new LightingSettingsParams { Action = "bogus" });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}

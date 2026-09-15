using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Lighting;

namespace Mosaic.Bridge.Tests.Lighting
{
    // O4 §4.3: bake type, cookie, culling mask, shadow bias, area size on an existing light —
    // previously only Color/Intensity/Range/SpotAngle/Shadows/ColorTemperature/BounceIntensity.
    [TestFixture]
    [Category("Lighting")]
    public class LightingSetPropertiesTests
    {
        private GameObject _lightGo;
        private const string CookiePath = "Assets/MosaicTestCookie.png";

        [SetUp]
        public void SetUp()
        {
            _lightGo = new GameObject("__MosaicTest_SetPropertiesLight__");
            _lightGo.AddComponent<Light>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_lightGo != null)
                Object.DestroyImmediate(_lightGo);
            if (AssetDatabase.LoadAssetAtPath<Texture>(CookiePath) != null)
                AssetDatabase.DeleteAsset(CookiePath);
        }

        [Test]
        public void Set_LightmapBakeTypeAndShadowBias_Apply()
        {
            var result = LightingSetPropertiesTool.Execute(new LightingSetPropertiesParams
            {
                Name = _lightGo.name, LightmapBakeType = "Baked", ShadowBias = 0.1f,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("Baked", result.Data.LightmapBakeType);
            Assert.AreEqual(0.1f, result.Data.ShadowBias, 0.0001f);
            Assert.AreEqual(LightmapBakeType.Baked, _lightGo.GetComponent<Light>().lightmapBakeType);
        }

        [Test]
        public void Set_Cookie_AssignsTextureAndClearsWithEmptyString()
        {
            // Point lights need a Cubemap cookie; Spot/Directional need a plain 2D texture —
            // assigning a 2D PNG to the default Point light silently drops back to null.
            _lightGo.GetComponent<Light>().type = LightType.Spot;

            var tex = new Texture2D(4, 4);
            File.WriteAllBytes(CookiePath, ImageConversion.EncodeToPNG(tex));
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(CookiePath, ImportAssetOptions.ForceSynchronousImport);

            var result = LightingSetPropertiesTool.Execute(new LightingSetPropertiesParams
            {
                Name = _lightGo.name, CookiePath = CookiePath,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(CookiePath, result.Data.CookiePath);
            Assert.IsNotNull(_lightGo.GetComponent<Light>().cookie);

            var cleared = LightingSetPropertiesTool.Execute(new LightingSetPropertiesParams
            {
                Name = _lightGo.name, CookiePath = "",
            });

            Assert.IsTrue(cleared.Success, cleared.Error);
            Assert.IsNull(_lightGo.GetComponent<Light>().cookie);
        }

        [Test]
        public void Set_CullingMask_AppliesLayerMask()
        {
            var result = LightingSetPropertiesTool.Execute(new LightingSetPropertiesParams
            {
                Name = _lightGo.name, CullingMask = "Default",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(LayerMask.GetMask("Default"), _lightGo.GetComponent<Light>().cullingMask);
        }

        [Test]
        public void Set_CookieSize_RequiresTwoElements()
        {
            var result = LightingSetPropertiesTool.Execute(new LightingSetPropertiesParams
            {
                Name = _lightGo.name, CookieSize = new[] { 1f },
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Set_InvalidLightmapBakeType_ReturnsFail()
        {
            var result = LightingSetPropertiesTool.Execute(new LightingSetPropertiesParams
            {
                Name = _lightGo.name, LightmapBakeType = "Bogus",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}

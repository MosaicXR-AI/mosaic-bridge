using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Mosaic.Bridge.Tools.Lighting;

namespace Mosaic.Bridge.Tests.Lighting
{
    [TestFixture]
    [Category("Lighting")]
    public class LightingSetEnvironmentTests
    {
        private Color _originalAmbientColor;
        private float _originalAmbientIntensity;
        private AmbientMode _originalAmbientMode;
        private bool _originalFog;
        private Color _originalFogColor;
        private float _originalFogDensity;

        [SetUp]
        public void SetUp()
        {
            _originalAmbientColor     = RenderSettings.ambientLight;
            _originalAmbientIntensity = RenderSettings.ambientIntensity;
            _originalAmbientMode      = RenderSettings.ambientMode;
            _originalFog              = RenderSettings.fog;
            _originalFogColor         = RenderSettings.fogColor;
            _originalFogDensity       = RenderSettings.fogDensity;
        }

        [TearDown]
        public void TearDown()
        {
            RenderSettings.ambientLight     = _originalAmbientColor;
            RenderSettings.ambientIntensity = _originalAmbientIntensity;
            RenderSettings.ambientMode      = _originalAmbientMode;
            RenderSettings.fog              = _originalFog;
            RenderSettings.fogColor         = _originalFogColor;
            RenderSettings.fogDensity       = _originalFogDensity;
        }

        [Test]
        public void SetEnvironment_AmbientColor_ChangesRenderSettings()
        {
            var result = LightingSetEnvironmentTool.Execute(new LightingSetEnvironmentParams
            {
                AmbientColor = new[] { 1f, 0f, 0f }
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.PropertiesChanged);
            Assert.AreEqual(1f, RenderSettings.ambientLight.r, 0.01f);
            Assert.AreEqual(0f, RenderSettings.ambientLight.g, 0.01f);
            Assert.AreEqual(0f, RenderSettings.ambientLight.b, 0.01f);
        }

        [Test]
        public void SetEnvironment_AmbientMode_ChangesRenderSettings()
        {
            var result = LightingSetEnvironmentTool.Execute(new LightingSetEnvironmentParams
            {
                AmbientMode = "Flat"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(AmbientMode.Flat, RenderSettings.ambientMode);
        }

        [Test]
        public void SetEnvironment_FogSettings_ChangesRenderSettings()
        {
            var result = LightingSetEnvironmentTool.Execute(new LightingSetEnvironmentParams
            {
                FogEnabled = true,
                FogColor   = new[] { 0.5f, 0.5f, 0.5f },
                FogDensity = 0.02f
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(3, result.Data.PropertiesChanged);
            Assert.IsTrue(RenderSettings.fog);
            Assert.AreEqual(0.5f, RenderSettings.fogColor.r, 0.01f);
            Assert.AreEqual(0.02f, RenderSettings.fogDensity, 0.001f);
        }

        [Test]
        public void SetEnvironment_InvalidAmbientMode_ReturnsFail()
        {
            var result = LightingSetEnvironmentTool.Execute(new LightingSetEnvironmentParams
            {
                AmbientMode = "InvalidMode"
            });

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Error.Contains("Invalid ambient mode"));
        }

        [Test]
        public void SetEnvironment_MultipleProperties_CountsCorrectly()
        {
            var result = LightingSetEnvironmentTool.Execute(new LightingSetEnvironmentParams
            {
                AmbientColor     = new[] { 0.2f, 0.3f, 0.4f },
                AmbientIntensity = 0.8f,
                FogEnabled       = false
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(3, result.Data.PropertiesChanged);
        }

        [Test]
        public void SetEnvironment_ReturnsCurrentState()
        {
            RenderSettings.ambientLight = Color.blue;
            RenderSettings.fog = true;

            var result = LightingSetEnvironmentTool.Execute(new LightingSetEnvironmentParams
            {
                AmbientIntensity = 1.5f
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0f, result.Data.AmbientColor[0], 0.01f); // r
            Assert.AreEqual(0f, result.Data.AmbientColor[1], 0.01f); // g
            Assert.AreEqual(1f, result.Data.AmbientColor[2], 0.01f); // b
            Assert.IsTrue(result.Data.FogEnabled);
        }
    }
}

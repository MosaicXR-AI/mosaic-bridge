using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Tools.AdvancedRendering;

namespace Mosaic.Bridge.Tests.Unit.Tools.Rendering
{
    /// <summary>
    /// Unit tests for the shader/ssr-configure tool (Story 26-5).
    /// Validates quality presets, error handling, and file generation.
    /// </summary>
    [TestFixture]
    [Category("Unit")]
    [Category("Rendering")]
    public class SsrConfigureTests
    {
        private const string TestOutputDir = "Assets/Generated/Rendering/Tests_SSR/";
        private string _fullOutputDir;

        [SetUp]
        public void SetUp()
        {
            _fullOutputDir = Path.Combine(Application.dataPath, "..", TestOutputDir);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_fullOutputDir))
            {
                Directory.Delete(_fullOutputDir, true);
                var metaFile = _fullOutputDir.TrimEnd('/', '\\') + ".meta";
                if (File.Exists(metaFile)) File.Delete(metaFile);
                AssetDatabase.Refresh();
            }
        }

        // ── Medium quality generates both shader + feature ──────────────────

        [Test]
        public void MediumQuality_CreatesShaderAndFeatureScript()
        {
            var result = ShaderSsrConfigureTool.Execute(new ShaderSsrConfigureParams
            {
                Quality    = "medium",
                OutputName = "Medium",
                SavePath   = TestOutputDir
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("medium", result.Data.Quality);
            Assert.AreEqual(64, result.Data.MaxSteps);
            Assert.IsTrue(result.Data.ShaderPath.EndsWith("SSR_Medium.shader"));
            Assert.IsTrue(result.Data.ScriptPath.EndsWith("SSRFeature_Medium.cs"));

            var shaderFull = Path.Combine(Application.dataPath, "..", result.Data.ShaderPath);
            var scriptFull = Path.Combine(Application.dataPath, "..", result.Data.ScriptPath);
            Assert.IsTrue(File.Exists(shaderFull), $"Expected shader at {shaderFull}");
            Assert.IsTrue(File.Exists(scriptFull), $"Expected feature script at {scriptFull}");
        }

        // ── All 4 qualities produce valid output with correct step counts ──

        [TestCase("low",    32)]
        [TestCase("medium", 64)]
        [TestCase("high",   128)]
        [TestCase("ultra",  256)]
        public void AllQualities_ProduceValidOutput(string quality, int expectedSteps)
        {
            var result = ShaderSsrConfigureTool.Execute(new ShaderSsrConfigureParams
            {
                Quality    = quality,
                OutputName = "Q_" + quality,
                SavePath   = TestOutputDir
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(quality, result.Data.Quality);
            Assert.AreEqual(expectedSteps, result.Data.MaxSteps);

            var shaderFull = Path.Combine(Application.dataPath, "..", result.Data.ShaderPath);
            Assert.IsTrue(File.Exists(shaderFull), $"Expected shader for {quality} at {shaderFull}");
        }

        // ── Invalid quality returns INVALID_PARAM ──────────────────────────

        [Test]
        public void InvalidQuality_ReturnsError()
        {
            var result = ShaderSsrConfigureTool.Execute(new ShaderSsrConfigureParams
            {
                Quality    = "potato",
                OutputName = "Bad",
                SavePath   = TestOutputDir
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ErrorCodes.INVALID_PARAM, result.ErrorCode);
        }

        // ── MaxSteps adjusts by quality preset when not overridden ────────

        [Test]
        public void MaxSteps_AdjustedByQuality()
        {
            var low = ShaderSsrConfigureTool.Execute(new ShaderSsrConfigureParams
            {
                Quality = "low", OutputName = "LowSteps", SavePath = TestOutputDir
            });
            var ultra = ShaderSsrConfigureTool.Execute(new ShaderSsrConfigureParams
            {
                Quality = "ultra", OutputName = "UltraSteps", SavePath = TestOutputDir
            });

            Assert.IsTrue(low.Success, low.Error);
            Assert.IsTrue(ultra.Success, ultra.Error);
            Assert.AreEqual(32, low.Data.MaxSteps);
            Assert.AreEqual(256, ultra.Data.MaxSteps);
            Assert.Less(low.Data.MaxSteps, ultra.Data.MaxSteps,
                "Ultra preset should have more steps than low preset");
        }

        // ── Explicit MaxSteps overrides quality preset ─────────────────────

        [Test]
        public void ExplicitMaxSteps_OverridesPreset()
        {
            var result = ShaderSsrConfigureTool.Execute(new ShaderSsrConfigureParams
            {
                Quality    = "low",
                MaxSteps   = 200,
                OutputName = "Override",
                SavePath   = TestOutputDir
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(200, result.Data.MaxSteps);
        }

        // ── Default (null quality) is medium ───────────────────────────────

        [Test]
        public void DefaultQuality_IsMedium()
        {
            var result = ShaderSsrConfigureTool.Execute(new ShaderSsrConfigureParams
            {
                OutputName = "DefaultQ",
                SavePath   = TestOutputDir
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("medium", result.Data.Quality);
            Assert.AreEqual(64, result.Data.MaxSteps);
        }

        // ── Generated shader contains SSR implementation markers ──────────

        [Test]
        public void GeneratedShader_ContainsSSRImplementation()
        {
            var result = ShaderSsrConfigureTool.Execute(new ShaderSsrConfigureParams
            {
                Quality    = "medium",
                OutputName = "ContentCheck",
                SavePath   = TestOutputDir
            });

            Assert.IsTrue(result.Success, result.Error);
            var shaderFull = Path.Combine(Application.dataPath, "..", result.Data.ShaderPath);
            var src = File.ReadAllText(shaderFull);

            StringAssert.Contains("TraceSSR", src);
            StringAssert.Contains("SampleSceneDepth", src);
            StringAssert.Contains("_CameraNormalsTexture", src);
            StringAssert.Contains("reflect", src);
        }

        // ── Bad SavePath returns INVALID_PARAM ─────────────────────────────

        [Test]
        public void InvalidSavePath_ReturnsError()
        {
            var result = ShaderSsrConfigureTool.Execute(new ShaderSsrConfigureParams
            {
                Quality    = "medium",
                OutputName = "BadPath",
                SavePath   = "/tmp/outside-assets/"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ErrorCodes.INVALID_PARAM, result.ErrorCode);
        }

        // ── Missing TargetCamera returns NOT_FOUND ─────────────────────────

        [Test]
        public void MissingTargetCamera_ReturnsNotFound()
        {
            var result = ShaderSsrConfigureTool.Execute(new ShaderSsrConfigureParams
            {
                Quality      = "medium",
                OutputName   = "NoCam",
                SavePath     = TestOutputDir,
                TargetCamera = "__this_camera_does_not_exist__"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ErrorCodes.NOT_FOUND, result.ErrorCode);
        }
    }
}

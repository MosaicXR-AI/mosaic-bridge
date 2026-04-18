using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Tools.AdvancedRendering;

namespace Mosaic.Bridge.Tests.Unit.Tools.Rendering
{
    [TestFixture]
    [Category("Unit")]
    public class VolumetricFogTests
    {
        private const string TestSavePath = "Assets/MosaicTestTemp/VolumetricFog/";

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder("Assets/MosaicTestTemp"))
                AssetDatabase.DeleteAsset("Assets/MosaicTestTemp");
        }

        private static string ProjectRoot => Application.dataPath.Replace("/Assets", "");

        [Test]
        public void Urp_CreatesScriptAndComputeShader()
        {
            var result = RenderingVolumetricFogTool.Execute(new RenderingVolumetricFogParams
            {
                Pipeline = "urp",
                Name = "UrpTest",
                SavePath = TestSavePath
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("urp", result.Data.Pipeline);
            Assert.IsNotNull(result.Data.ScriptPath);
            Assert.IsNotNull(result.Data.ComputeShaderPath);
            Assert.IsTrue(File.Exists(Path.Combine(ProjectRoot, result.Data.ScriptPath)));
            Assert.IsTrue(File.Exists(Path.Combine(ProjectRoot, result.Data.ComputeShaderPath)));
            Assert.IsTrue(result.Data.ScriptPath.EndsWith("VolumetricFogFeature_UrpTest.cs"));
            Assert.IsTrue(result.Data.ComputeShaderPath.EndsWith("VolumetricFog_UrpTest.compute"));
        }

        [Test]
        public void Hdrp_CreatesScript()
        {
            var result = RenderingVolumetricFogTool.Execute(new RenderingVolumetricFogParams
            {
                Pipeline = "hdrp",
                Name = "HdrpTest",
                SavePath = TestSavePath
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("hdrp", result.Data.Pipeline);
            Assert.IsTrue(File.Exists(Path.Combine(ProjectRoot, result.Data.ScriptPath)));
            Assert.IsTrue(File.Exists(Path.Combine(ProjectRoot, result.Data.ComputeShaderPath)));
        }

        [Test]
        public void Builtin_CreatesScript()
        {
            var result = RenderingVolumetricFogTool.Execute(new RenderingVolumetricFogParams
            {
                Pipeline = "builtin",
                Name = "BuiltinTest",
                SavePath = TestSavePath
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("builtin", result.Data.Pipeline);
            Assert.IsTrue(File.Exists(Path.Combine(ProjectRoot, result.Data.ScriptPath)));
            Assert.IsTrue(File.Exists(Path.Combine(ProjectRoot, result.Data.ComputeShaderPath)));
        }

        [Test]
        public void InvalidPipeline_ReturnsError()
        {
            var result = RenderingVolumetricFogTool.Execute(new RenderingVolumetricFogParams
            {
                Pipeline = "bogus",
                Name = "BadPipe",
                SavePath = TestSavePath
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ErrorCodes.INVALID_PARAM, result.ErrorCode);
        }

        [Test]
        public void InvalidResolutionLength_ReturnsError()
        {
            var result = RenderingVolumetricFogTool.Execute(new RenderingVolumetricFogParams
            {
                Pipeline = "urp",
                Resolution = new[] { 160, 90 },
                Name = "BadRes",
                SavePath = TestSavePath
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ErrorCodes.INVALID_PARAM, result.ErrorCode);
        }

        [Test]
        public void NonPositiveResolution_ReturnsError()
        {
            var result = RenderingVolumetricFogTool.Execute(new RenderingVolumetricFogParams
            {
                Pipeline = "urp",
                Resolution = new[] { 160, 0, 64 },
                Name = "ZeroRes",
                SavePath = TestSavePath
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ErrorCodes.INVALID_PARAM, result.ErrorCode);
        }

        [Test]
        public void InvalidSavePath_ReturnsError()
        {
            var result = RenderingVolumetricFogTool.Execute(new RenderingVolumetricFogParams
            {
                Pipeline = "urp",
                Name = "BadPath",
                SavePath = "NotAssets/fog/"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ErrorCodes.INVALID_PARAM, result.ErrorCode);
        }

        [Test]
        public void InvalidFogColor_ReturnsError()
        {
            var result = RenderingVolumetricFogTool.Execute(new RenderingVolumetricFogParams
            {
                Pipeline = "urp",
                FogColor = new[] { 0.5f, 0.5f, 0.5f },
                Name = "BadColor",
                SavePath = TestSavePath
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ErrorCodes.INVALID_PARAM, result.ErrorCode);
        }

        [Test]
        public void ReturnsResolutionArray()
        {
            var result = RenderingVolumetricFogTool.Execute(new RenderingVolumetricFogParams
            {
                Pipeline = "urp",
                Resolution = new[] { 64, 36, 32 },
                Name = "ResCheck",
                SavePath = TestSavePath
            });

            Assert.IsTrue(result.Success);
            Assert.AreEqual(3, result.Data.Resolution.Length);
            Assert.AreEqual(64, result.Data.Resolution[0]);
            Assert.AreEqual(36, result.Data.Resolution[1]);
            Assert.AreEqual(32, result.Data.Resolution[2]);
        }
    }
}

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
    public class RadianceCascadesTests
    {
        private const string TestSavePath = "Assets/MosaicTestTemp/RadianceCascades/";

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
            var result = RenderingRadianceCascadesTool.Execute(new RenderingRadianceCascadesParams
            {
                Pipeline   = "urp",
                OutputName = "UrpTest",
                SavePath   = TestSavePath
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("urp", result.Data.Pipeline);
            Assert.AreEqual(6, result.Data.CascadeCount);
            Assert.IsNotNull(result.Data.ScriptPath);
            Assert.IsNotNull(result.Data.ComputeShaderPath);
            Assert.IsTrue(File.Exists(Path.Combine(ProjectRoot, result.Data.ScriptPath)));
            Assert.IsTrue(File.Exists(Path.Combine(ProjectRoot, result.Data.ComputeShaderPath)));
            Assert.IsTrue(result.Data.ScriptPath.EndsWith("RadianceCascadesFeature_UrpTest.cs"));
            Assert.IsTrue(result.Data.ComputeShaderPath.EndsWith("RadianceCascades_UrpTest.compute"));
        }

        [Test]
        public void Hdrp_CreatesScriptAndComputeShader()
        {
            var result = RenderingRadianceCascadesTool.Execute(new RenderingRadianceCascadesParams
            {
                Pipeline   = "hdrp",
                OutputName = "HdrpTest",
                SavePath   = TestSavePath
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("hdrp", result.Data.Pipeline);
            Assert.IsTrue(File.Exists(Path.Combine(ProjectRoot, result.Data.ScriptPath)));
            Assert.IsTrue(File.Exists(Path.Combine(ProjectRoot, result.Data.ComputeShaderPath)));
        }

        [Test]
        public void InvalidPipeline_ReturnsError()
        {
            var result = RenderingRadianceCascadesTool.Execute(new RenderingRadianceCascadesParams
            {
                Pipeline   = "builtin",
                OutputName = "Bogus",
                SavePath   = TestSavePath
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ErrorCodes.INVALID_PARAM, result.ErrorCode);
        }

        [Test]
        public void ZeroCascadeCount_ReturnsError()
        {
            var result = RenderingRadianceCascadesTool.Execute(new RenderingRadianceCascadesParams
            {
                Pipeline     = "urp",
                CascadeCount = 0,
                OutputName   = "ZeroCasc",
                SavePath     = TestSavePath
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ErrorCodes.INVALID_PARAM, result.ErrorCode);
        }

        [Test]
        public void ExcessiveCascadeCount_ReturnsError()
        {
            var result = RenderingRadianceCascadesTool.Execute(new RenderingRadianceCascadesParams
            {
                Pipeline     = "urp",
                CascadeCount = 99,
                OutputName   = "HugeCasc",
                SavePath     = TestSavePath
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ErrorCodes.INVALID_PARAM, result.ErrorCode);
        }

        [Test]
        public void NonPositiveProbeSpacing_ReturnsError()
        {
            var result = RenderingRadianceCascadesTool.Execute(new RenderingRadianceCascadesParams
            {
                Pipeline     = "urp",
                ProbeSpacing = 0f,
                OutputName   = "ZeroSpace",
                SavePath     = TestSavePath
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ErrorCodes.INVALID_PARAM, result.ErrorCode);
        }

        [Test]
        public void InvalidResolutionLength_ReturnsError()
        {
            var result = RenderingRadianceCascadesTool.Execute(new RenderingRadianceCascadesParams
            {
                Pipeline   = "urp",
                Resolution = new[] { 1920 },
                OutputName = "BadRes",
                SavePath   = TestSavePath
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ErrorCodes.INVALID_PARAM, result.ErrorCode);
        }

        [Test]
        public void InvalidSavePath_ReturnsError()
        {
            var result = RenderingRadianceCascadesTool.Execute(new RenderingRadianceCascadesParams
            {
                Pipeline   = "urp",
                OutputName = "BadPath",
                SavePath   = "NotAssets/rc/"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual(ErrorCodes.INVALID_PARAM, result.ErrorCode);
        }

        [Test]
        public void CustomCascadeCount_Propagates()
        {
            var result = RenderingRadianceCascadesTool.Execute(new RenderingRadianceCascadesParams
            {
                Pipeline     = "urp",
                CascadeCount = 4,
                OutputName   = "Four",
                SavePath     = TestSavePath
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(4, result.Data.CascadeCount);
        }
    }
}

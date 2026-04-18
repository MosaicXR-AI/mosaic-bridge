using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Mosaic.Bridge.Tools.AdvancedRendering;

namespace Mosaic.Bridge.Tests.Unit.Tools.Rendering
{
    [TestFixture]
    [Category("Unit")]
    [Category("Rendering")]
    public class ParallaxCreateTests
    {
        const string TestDir = "Assets/Generated/RenderingTests/";

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TestDir.TrimEnd('/')))
            {
                AssetDatabase.DeleteAsset(TestDir.TrimEnd('/'));
            }
        }

        [Test]
        public void Urp_Pipeline_CreatesShaderAndMaterial()
        {
            var result = ShaderParallaxCreateTool.Execute(new ShaderParallaxCreateParams
            {
                Pipeline   = "urp",
                OutputName = "POM_URP_Test",
                SavePath   = TestDir
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data);
            Assert.IsTrue(result.Data.ShaderPath.EndsWith(".shader"));
            Assert.IsTrue(result.Data.MaterialPath.EndsWith(".mat"));
            Assert.IsTrue(File.Exists(result.Data.ShaderPath), "Shader file should exist on disk");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(result.Data.MaterialPath);
            Assert.IsNotNull(mat, "Material should load from AssetDatabase");
            Assert.AreEqual(0.05f, result.Data.HeightScale, 1e-4f);
        }

        [Test]
        public void Builtin_Pipeline_CreatesShaderAndMaterial()
        {
            var result = ShaderParallaxCreateTool.Execute(new ShaderParallaxCreateParams
            {
                Pipeline   = "builtin",
                OutputName = "POM_Builtin_Test",
                SavePath   = TestDir
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(File.Exists(result.Data.ShaderPath));
            var mat = AssetDatabase.LoadAssetAtPath<Material>(result.Data.MaterialPath);
            Assert.IsNotNull(mat);
        }

        [Test]
        public void InvalidPipeline_ReturnsError()
        {
            var result = ShaderParallaxCreateTool.Execute(new ShaderParallaxCreateParams
            {
                Pipeline   = "hdrp-custom",
                OutputName = "POM_Invalid_Test",
                SavePath   = TestDir
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void MinStepsGreaterOrEqualMaxSteps_ReturnsError()
        {
            var result = ShaderParallaxCreateTool.Execute(new ShaderParallaxCreateParams
            {
                Pipeline   = "urp",
                MinSteps   = 32,
                MaxSteps   = 32,
                OutputName = "POM_Steps_Test",
                SavePath   = TestDir
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
            StringAssert.Contains("MinSteps", result.Error);
        }

        [Test]
        public void GeneratedShader_ContainsPomAndValidStructure()
        {
            var result = ShaderParallaxCreateTool.Execute(new ShaderParallaxCreateParams
            {
                Pipeline   = "urp",
                SelfShadow = true,
                OutputName = "POM_Valid_Test",
                SavePath   = TestDir
            });

            Assert.IsTrue(result.Success, result.Error);
            var src = File.ReadAllText(result.Data.ShaderPath);

            // Balanced braces indicates structurally valid shader source
            int open = 0, close = 0;
            foreach (var ch in src)
            {
                if (ch == '{') open++;
                else if (ch == '}') close++;
            }
            Assert.AreEqual(open, close, "Shader braces must be balanced");

            StringAssert.Contains("Shader \"", src);
            StringAssert.Contains("SubShader", src);
            StringAssert.Contains("ParallaxOcclusion", src);
            StringAssert.Contains("ParallaxShadow", src);
            StringAssert.Contains("_Heightmap", src);
        }
    }
}

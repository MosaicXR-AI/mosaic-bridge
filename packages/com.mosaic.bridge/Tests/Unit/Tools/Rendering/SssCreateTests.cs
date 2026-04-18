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
    public class SssCreateTests
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

        static void AssertPresetCreates(string profile, string name)
        {
            var result = ShaderSssCreateTool.Execute(new ShaderSssCreateParams
            {
                Profile    = profile,
                Pipeline   = "urp",
                OutputName = name,
                SavePath   = TestDir
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data);
            Assert.AreEqual(profile, result.Data.Profile);
            Assert.IsTrue(result.Data.ShaderPath.EndsWith(".shader"));
            Assert.IsTrue(result.Data.MaterialPath.EndsWith(".mat"));
            Assert.IsTrue(File.Exists(result.Data.ShaderPath), "Shader file should exist on disk");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(result.Data.MaterialPath);
            Assert.IsNotNull(mat, "Material should load from AssetDatabase");
        }

        [Test] public void Preset_Skin_CreatesShaderAndMaterial()    => AssertPresetCreates("skin",    "SSS_Skin_Test");
        [Test] public void Preset_Foliage_CreatesShaderAndMaterial() => AssertPresetCreates("foliage", "SSS_Foliage_Test");
        [Test] public void Preset_Wax_CreatesShaderAndMaterial()     => AssertPresetCreates("wax",     "SSS_Wax_Test");
        [Test] public void Preset_Ice_CreatesShaderAndMaterial()     => AssertPresetCreates("ice",     "SSS_Ice_Test");
        [Test] public void Preset_Custom_CreatesShaderAndMaterial()  => AssertPresetCreates("custom",  "SSS_Custom_Test");

        [Test]
        public void InvalidProfile_ReturnsError()
        {
            var result = ShaderSssCreateTool.Execute(new ShaderSssCreateParams
            {
                Profile    = "marble",
                Pipeline   = "urp",
                OutputName = "SSS_InvalidProfile_Test",
                SavePath   = TestDir
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
            StringAssert.Contains("profile", result.Error.ToLower());
        }

        [Test]
        public void InvalidPipeline_ReturnsError()
        {
            var result = ShaderSssCreateTool.Execute(new ShaderSssCreateParams
            {
                Profile    = "skin",
                Pipeline   = "hdrp-custom",
                OutputName = "SSS_InvalidPipeline_Test",
                SavePath   = TestDir
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void GeneratedShader_FilesExistAndStructurallyValid()
        {
            var result = ShaderSssCreateTool.Execute(new ShaderSssCreateParams
            {
                Profile    = "skin",
                Pipeline   = "urp",
                OutputName = "SSS_Struct_Test",
                SavePath   = TestDir
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(File.Exists(result.Data.ShaderPath));
            var src = File.ReadAllText(result.Data.ShaderPath);

            int open = 0, close = 0;
            foreach (var ch in src)
            {
                if (ch == '{') open++;
                else if (ch == '}') close++;
            }
            Assert.AreEqual(open, close, "Shader braces must be balanced");

            StringAssert.Contains("Shader \"", src);
            StringAssert.Contains("SubShader", src);
            StringAssert.Contains("_ScatterColor", src);
            StringAssert.Contains("_Thickness", src);
            StringAssert.Contains("_Wrap", src);
        }
    }
}

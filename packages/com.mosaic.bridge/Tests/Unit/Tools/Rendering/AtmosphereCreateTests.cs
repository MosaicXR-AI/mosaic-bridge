using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Mosaic.Bridge.Tools.AdvancedRendering;

namespace Mosaic.Bridge.Tests.Unit.Tools.Rendering
{
    /// <summary>
    /// Unit tests for the rendering/atmosphere-create tool (Story 26-1).
    /// </summary>
    [TestFixture]
    [Category("Unit")]
    [Category("Rendering")]
    public class AtmosphereCreateTests
    {
        private const string TestDir = "Assets/Generated/Rendering/_AtmosphereTests/";

        [SetUp]
        public void SetUp()
        {
            if (AssetDatabase.IsValidFolder(TestDir.TrimEnd('/')))
                AssetDatabase.DeleteAsset(TestDir.TrimEnd('/'));
            Directory.CreateDirectory(Path.Combine(Application.dataPath.Replace("/Assets", ""), TestDir));
            AssetDatabase.Refresh();
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TestDir.TrimEnd('/')))
                AssetDatabase.DeleteAsset(TestDir.TrimEnd('/'));
            AssetDatabase.Refresh();
        }

        static string FullPath(string assetPath)
            => Path.Combine(Application.dataPath.Replace("/Assets", ""), assetPath);

        [Test]
        public void Preetham_CreatesShaderAndMaterial()
        {
            var result = RenderingAtmosphereCreateTool.Execute(new RenderingAtmosphereCreateParams
            {
                Model      = "preetham",
                OutputType = "skybox_material",
                OutputName = "Atmo_Preetham_Test",
                SavePath   = TestDir,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("preetham", result.Data.Model);
            Assert.AreEqual("skybox_material", result.Data.OutputType);
            Assert.IsNotNull(result.Data.ShaderPath);
            Assert.IsNotNull(result.Data.MaterialPath);
            Assert.IsFalse(result.Data.AppliedToScene);
            Assert.IsTrue(File.Exists(FullPath(result.Data.ShaderPath)), "Shader file should exist on disk");
            Assert.IsTrue(File.Exists(FullPath(result.Data.MaterialPath)), "Material file should exist on disk");
        }

        [Test]
        public void Bruneton_CreatesShader()
        {
            var result = RenderingAtmosphereCreateTool.Execute(new RenderingAtmosphereCreateParams
            {
                Model      = "bruneton",
                OutputType = "shader",
                OutputName = "Atmo_Bruneton_Test",
                SavePath   = TestDir,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("bruneton", result.Data.Model);
            Assert.AreEqual("shader", result.Data.OutputType);
            Assert.IsNotNull(result.Data.ShaderPath);
            Assert.IsTrue(File.Exists(FullPath(result.Data.ShaderPath)), "Bruneton shader file should exist");
        }

        [Test]
        public void Oneil_CreatesShader()
        {
            var result = RenderingAtmosphereCreateTool.Execute(new RenderingAtmosphereCreateParams
            {
                Model      = "oneil",
                OutputType = "shader",
                OutputName = "Atmo_Oneil_Test",
                SavePath   = TestDir,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("oneil", result.Data.Model);
            Assert.AreEqual("shader", result.Data.OutputType);
            Assert.IsTrue(File.Exists(FullPath(result.Data.ShaderPath)), "O'Neil shader file should exist");
        }

        [Test]
        public void InvalidModel_ReturnsError()
        {
            var result = RenderingAtmosphereCreateTool.Execute(new RenderingAtmosphereCreateParams
            {
                Model      = "nishita",
                OutputType = "skybox_material",
                OutputName = "Atmo_Invalid_Model",
                SavePath   = TestDir,
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void InvalidOutputType_ReturnsError()
        {
            var result = RenderingAtmosphereCreateTool.Execute(new RenderingAtmosphereCreateParams
            {
                Model      = "preetham",
                OutputType = "png_texture",
                OutputName = "Atmo_Invalid_Output",
                SavePath   = TestDir,
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void GeneratedShaderFile_ExistsOnDisk()
        {
            var result = RenderingAtmosphereCreateTool.Execute(new RenderingAtmosphereCreateParams
            {
                Model      = "preetham",
                OutputType = "shader",
                OutputName = "Atmo_FileExists_Test",
                SavePath   = TestDir,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data.ShaderPath);
            Assert.IsTrue(result.Data.ShaderPath.EndsWith(".shader"));
            Assert.IsTrue(File.Exists(FullPath(result.Data.ShaderPath)),
                $"Expected shader at '{result.Data.ShaderPath}' to exist on disk");

            // Basic content sanity check: the file should reference Shader block
            var contents = File.ReadAllText(FullPath(result.Data.ShaderPath));
            StringAssert.Contains("Shader \"Mosaic/Atmosphere/", contents);
        }
    }
}

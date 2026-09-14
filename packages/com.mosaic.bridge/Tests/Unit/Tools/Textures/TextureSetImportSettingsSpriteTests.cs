using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Textures;

namespace Mosaic.Bridge.Tests.Unit.Tools.Textures
{
    // O4 §4.1 (G1): texture/set-import-settings could set TextureType=Sprite but not
    // spriteImportMode/PPU/pivot/border — BS:477's own finding was that a UI Image's Sliced type
    // does nothing until spriteBorder is non-zero on the SPRITE, not the Image. Real asset,
    // real reimport — sprite import settings are notorious for silently no-op'ing when set in the
    // wrong order or on the wrong object.
    [TestFixture]
    [Category("Textures")]
    public class TextureSetImportSettingsSpriteTests
    {
        private const string TestTexturePath = "Assets/MosaicBridgeTests_TempSprite.png";

        [SetUp]
        public void SetUp()
        {
            var tex = new Texture2D(8, 8);
            var pixels = new Color32[64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 0, 0, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(TestTexturePath, ImageConversion.EncodeToPNG(tex));
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(TestTexturePath, ImportAssetOptions.ForceSynchronousImport);

            // Sprite-only params require TextureType=Sprite already set.
            TextureSetImportSettingsTool.SetImportSettings(new TextureSetImportSettingsParams
            {
                AssetPath = TestTexturePath, TextureType = "Sprite"
            });
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestTexturePath);
        }

        [Test]
        public void SetSpriteMode_Multiple_IsApplied()
        {
            var result = TextureSetImportSettingsTool.SetImportSettings(new TextureSetImportSettingsParams
            {
                AssetPath = TestTexturePath, SpriteMode = "Multiple"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("Multiple", result.Data.SpriteMode);
        }

        [Test]
        public void SetPixelsPerUnit_IsApplied()
        {
            var result = TextureSetImportSettingsTool.SetImportSettings(new TextureSetImportSettingsParams
            {
                AssetPath = TestTexturePath, PixelsPerUnit = 32f
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(32f, result.Data.PixelsPerUnit, 0.001f);
        }

        [Test]
        public void SetPivot_IsAppliedAndReadableBackFromTheRealImporter()
        {
            var result = TextureSetImportSettingsTool.SetImportSettings(new TextureSetImportSettingsParams
            {
                AssetPath = TestTexturePath, Pivot = new[] { 0.25f, 0.75f }
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0.25f, result.Data.Pivot[0], 0.001f);
            Assert.AreEqual(0.75f, result.Data.Pivot[1], 0.001f);

            // Re-read from a fresh importer instance — proves this survived SaveAndReimport, not
            // just the in-memory importer object this call happened to hold.
            var reimported = AssetImporter.GetAtPath(TestTexturePath) as TextureImporter;
            Assert.AreEqual(0.25f, reimported.spritePivot.x, 0.001f);
            Assert.AreEqual(0.75f, reimported.spritePivot.y, 0.001f);
        }

        [Test]
        public void SetPivot_WrongLength_Fails()
        {
            var result = TextureSetImportSettingsTool.SetImportSettings(new TextureSetImportSettingsParams
            {
                AssetPath = TestTexturePath, Pivot = new[] { 0.5f }
            });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void SetBorder_IsAppliedAndSurvivesReimport()
        {
            var result = TextureSetImportSettingsTool.SetImportSettings(new TextureSetImportSettingsParams
            {
                AssetPath = TestTexturePath, Border = new[] { 2f, 3f, 4f, 5f }
            });

            Assert.IsTrue(result.Success, result.Error);
            CollectionAssert.AreEqual(new[] { 2f, 3f, 4f, 5f }, result.Data.Border);

            var reimported = AssetImporter.GetAtPath(TestTexturePath) as TextureImporter;
            Assert.AreEqual(new Vector4(2f, 3f, 4f, 5f), reimported.spriteBorder);
        }

        [Test]
        public void SetMeshType_Tight_IsApplied()
        {
            var result = TextureSetImportSettingsTool.SetImportSettings(new TextureSetImportSettingsParams
            {
                AssetPath = TestTexturePath, MeshType = "Tight"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("Tight", result.Data.MeshType);
        }

        [Test]
        public void SetMeshType_Invalid_Fails()
        {
            var result = TextureSetImportSettingsTool.SetImportSettings(new TextureSetImportSettingsParams
            {
                AssetPath = TestTexturePath, MeshType = "NotAReal MeshType"
            });

            Assert.IsFalse(result.Success);
        }
    }
}

#if MOSAIC_HAS_2D_SPRITE
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Sprite2D;

namespace Mosaic.Bridge.Tests.Unit.Tools.Sprite2D
{
    // O4 §4.1 (G1): "D7's hidden prerequisite (session log: slicing was done by editor script)".
    // This is the riskiest route in the whole O4 push — UnityEditor.U2D.Sprites is a real but
    // rarely-scripted API surface, and a wrong exact-name/ordering assumption compiles fine and
    // fails only at runtime. These tests slice a real texture and read the resulting Sprite
    // sub-assets back from AssetDatabase, not just check the tool's own reported success.
    [TestFixture]
    [Category("Sprite2D")]
    public class SpriteSliceToolTests
    {
        private const string TestSheetPath = "Assets/MosaicBridgeTests_SliceSheet.png";

        [SetUp]
        public void SetUp()
        {
            // 16x8 texture: a 2x1 grid of 8x8 cells.
            var tex = new Texture2D(16, 8);
            var pixels = new Color32[128];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 200, 0, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(TestSheetPath, ImageConversion.EncodeToPNG(tex));
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(TestSheetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(TestSheetPath);
        }

        [Test]
        public void Grid_SlicesIntoExpectedCellCountAndSetsMultipleImportMode()
        {
            var result = SpriteSliceTool.Execute(new SpriteSliceParams
            {
                AssetPath = TestSheetPath, Mode = "Grid", CellSize = new[] { 8f, 8f },
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(2, result.Data.SpriteCount);

            var importer = (TextureImporter)AssetImporter.GetAtPath(TestSheetPath);
            Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
            Assert.AreEqual(SpriteImportMode.Multiple, importer.spriteImportMode);

            var sprites = AssetDatabase.LoadAllAssetsAtPath(TestSheetPath).OfType<Sprite>().ToArray();
            Assert.AreEqual(2, sprites.Length, "the real sub-assets on disk must match what the tool reported");
        }

        [Test]
        public void Grid_RowZeroIsTheVisuallyTopRow()
        {
            // Texture pixel space is Y-up with origin bottom-left; row 0 of the request must map
            // to the HIGHEST y — the same top-to-bottom convention tilemap/set-tiles' AsciiMap
            // uses. A 16x8 texture sliced into two 8x8 cells side by side (1 row) doesn't exercise
            // this, so slice with a taller texture instead: re-slice with a 4x4 cell size to get 2 rows.
            var result = SpriteSliceTool.Execute(new SpriteSliceParams
            {
                AssetPath = TestSheetPath, Mode = "Grid", CellSize = new[] { 16f, 4f },
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(2, result.Data.SpriteCount, "16x8 texture / 16x4 cells = 1 col x 2 rows");
            // index 0 (row 0, the request's own "top") must have the HIGHER y of the two.
            var first = result.Data.Sprites.First(s => s.Name.EndsWith("_0"));
            var second = result.Data.Sprites.First(s => s.Name.EndsWith("_1"));
            Assert.Greater(first.Rect[1], second.Rect[1],
                "row 0 (the visually top row) must have a higher texture-space y than row 1");
        }

        [Test]
        public void Grid_MissingCellSize_Fails()
        {
            var result = SpriteSliceTool.Execute(new SpriteSliceParams { AssetPath = TestSheetPath, Mode = "Grid" });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void Grid_CellSizeLargerThanTexture_Fails()
        {
            var result = SpriteSliceTool.Execute(new SpriteSliceParams
            {
                AssetPath = TestSheetPath, Mode = "Grid", CellSize = new[] { 100f, 100f },
            });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void Explicit_SlicesNamedRectsWithCustomPivot()
        {
            var result = SpriteSliceTool.Execute(new SpriteSliceParams
            {
                AssetPath = TestSheetPath, Mode = "Explicit",
                Rects = new[]
                {
                    new SpriteSliceRectParam { Name = "Left", X = 0, Y = 0, W = 8, H = 8 },
                    new SpriteSliceRectParam { Name = "Right", X = 8, Y = 0, W = 8, H = 8, Pivot = new[] { 0f, 0f } },
                },
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(2, result.Data.SpriteCount);

            var sprites = AssetDatabase.LoadAllAssetsAtPath(TestSheetPath).OfType<Sprite>()
                .ToDictionary(s => s.name);
            Assert.IsTrue(sprites.ContainsKey("Left"));
            Assert.IsTrue(sprites.ContainsKey("Right"));
        }

        [Test]
        public void Explicit_MissingRects_Fails()
        {
            var result = SpriteSliceTool.Execute(new SpriteSliceParams { AssetPath = TestSheetPath, Mode = "Explicit" });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void ReSlice_OnAnAlreadySlicedSheet_StillProducesTheRequestedCount()
        {
            // The specific regression class SetNameFileIdPairs exists to prevent: slicing an
            // ALREADY-sliced sheet a second time with a different rect count.
            SpriteSliceTool.Execute(new SpriteSliceParams
            {
                AssetPath = TestSheetPath, Mode = "Grid", CellSize = new[] { 8f, 8f },
            });

            var result = SpriteSliceTool.Execute(new SpriteSliceParams
            {
                AssetPath = TestSheetPath, Mode = "Grid", CellSize = new[] { 4f, 4f },
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(8, result.Data.SpriteCount, "16x8 / 4x4 cells = 4 cols x 2 rows");
            var sprites = AssetDatabase.LoadAllAssetsAtPath(TestSheetPath).OfType<Sprite>().ToArray();
            Assert.AreEqual(8, sprites.Length, "the re-slice must fully replace the previous slice, not merge with it");
        }

        [Test]
        public void UnknownMode_Fails()
        {
            var result = SpriteSliceTool.Execute(new SpriteSliceParams { AssetPath = TestSheetPath, Mode = "NotAMode" });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void MissingAsset_Fails()
        {
            var result = SpriteSliceTool.Execute(new SpriteSliceParams
            {
                AssetPath = "Assets/DoesNotExist_98765.png", Mode = "Grid", CellSize = new[] { 8f, 8f },
            });

            Assert.IsFalse(result.Success);
        }
    }
}
#endif

using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Sprites;

namespace Mosaic.Bridge.Tests.Unit.Tools.Sprites
{
    // O4 §4.1 (G1): sprite/info's whole purpose is letting an agent trust what Sprite.pivot/rect
    // actually report — Sprite.pivot is PIXEL-space relative to the sprite's own rect, not the
    // normalized 0..1 space TextureImporter.spritePivot uses, which is exactly the kind of
    // same-sounding-different-shape API mismatch this codebase has been burned by before.
    [TestFixture]
    [Category("Sprites")]
    public class SpriteToolTests
    {
        private const string TestSpritePath = "Assets/MosaicBridgeTests_SpriteTool.png";
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            var tex = new Texture2D(8, 8);
            var pixels = new Color32[64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 0, 255, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(TestSpritePath, ImageConversion.EncodeToPNG(tex));
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(TestSpritePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TestSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16f;
            importer.SaveAndReimport();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
                _go = null;
            }
            AssetDatabase.DeleteAsset(TestSpritePath);
        }

        // ── sprite/info ──────────────────────────────────────────────────────

        [Test]
        public void Info_SingleSprite_ReportsRectAndPixelsPerUnit()
        {
            var result = SpriteInfoTool.Execute(new SpriteInfoParams { AssetPath = TestSpritePath });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.Sprites.Length);
            var entry = result.Data.Sprites[0];
            Assert.AreEqual(8f, entry.Rect[2], 0.01f, "rect width");
            Assert.AreEqual(8f, entry.Rect[3], 0.01f, "rect height");
            Assert.AreEqual(16f, entry.PixelsPerUnit, 0.01f);
        }

        [Test]
        public void Info_MissingAsset_Fails()
        {
            var result = SpriteInfoTool.Execute(new SpriteInfoParams { AssetPath = "Assets/DoesNotExist_12345.png" });

            Assert.IsFalse(result.Success);
        }

        // ── sprite/create ────────────────────────────────────────────────────

        [Test]
        public void Create_MakesGameObjectWithSpriteRenderer()
        {
            var result = SpriteCreateTool.Execute(new SpriteCreateParams
            {
                Name = "MySprite", SpritePath = TestSpritePath
            });

            Assert.IsTrue(result.Success, result.Error);
            _go = GameObject.Find("MySprite");
            Assert.IsNotNull(_go);
            var renderer = _go.GetComponent<SpriteRenderer>();
            Assert.IsNotNull(renderer);
            Assert.IsNotNull(renderer.sprite);
        }

        [Test]
        public void Create_SetsColorFlipSortingAndPosition()
        {
            var result = SpriteCreateTool.Execute(new SpriteCreateParams
            {
                Name = "MySprite2", SpritePath = TestSpritePath,
                Color = new[] { 1f, 0f, 0f, 0.5f }, FlipX = true,
                SortingLayerName = "Default", SortingOrder = 5,
                Position = new[] { 1f, 2f, 3f },
            });

            Assert.IsTrue(result.Success, result.Error);
            _go = GameObject.Find("MySprite2");
            var renderer = _go.GetComponent<SpriteRenderer>();
            Assert.AreEqual(new Color(1f, 0f, 0f, 0.5f), renderer.color);
            Assert.IsTrue(renderer.flipX);
            Assert.AreEqual(5, renderer.sortingOrder);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), _go.transform.position);
        }

        [Test]
        public void Create_AutoColliderBox_AddsBoxCollider2D()
        {
            var result = SpriteCreateTool.Execute(new SpriteCreateParams
            {
                Name = "MySprite3", SpritePath = TestSpritePath, AutoCollider = "Box"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("Box", result.Data.ColliderType);
            _go = GameObject.Find("MySprite3");
            Assert.IsNotNull(_go.GetComponent<BoxCollider2D>());
        }

        [Test]
        public void Create_SlicedDrawModeWithSize_SetsRendererSize()
        {
            var result = SpriteCreateTool.Execute(new SpriteCreateParams
            {
                Name = "MySprite4", SpritePath = TestSpritePath, DrawMode = "Sliced", Size = new[] { 3f, 4f }
            });

            Assert.IsTrue(result.Success, result.Error);
            _go = GameObject.Find("MySprite4");
            var renderer = _go.GetComponent<SpriteRenderer>();
            Assert.AreEqual(SpriteDrawMode.Sliced, renderer.drawMode);
            Assert.AreEqual(new Vector2(3f, 4f), renderer.size);
        }

        [Test]
        public void Create_MissingSpritePath_Fails()
        {
            var result = SpriteCreateTool.Execute(new SpriteCreateParams { Name = "NoSprite" });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void Create_InvalidDrawMode_Fails()
        {
            var result = SpriteCreateTool.Execute(new SpriteCreateParams
            {
                Name = "BadMode", SpritePath = TestSpritePath, DrawMode = "NotAMode"
            });

            Assert.IsFalse(result.Success);
        }
    }
}

using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Physics2D;

namespace Mosaic.Bridge.Tests.Unit.Tools.Physics2D
{
    // O4 §4.1 (G1): mirrors of the 3D physics/add-rigidbody and physics/add-collider tools.
    [TestFixture]
    [Category("Physics2D")]
    public class Physics2DToolTests
    {
        private const string TestSpritePath = "Assets/MosaicBridgeTests_Physics2DSprite.png";
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            var tex = new Texture2D(8, 4);
            var pixels = new Color32[32];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 0, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(TestSpritePath, ImageConversion.EncodeToPNG(tex));
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(TestSpritePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TestSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 8f; // 8x4 px / 8 PPU -> 1x0.5 world units
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

        // ── physics2d/add-rigidbody ──────────────────────────────────────────

        [Test]
        public void AddRigidbody_SetsBodyTypeMassAndGravityScale()
        {
            _go = new GameObject("RB2DTest");

            var result = Physics2DAddRigidbodyTool.Execute(new Physics2DAddRigidbodyParams
            {
                Name = "RB2DTest", BodyType = "Kinematic", Mass = 3f, GravityScale = 0.5f
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("Kinematic", result.Data.BodyType);
            var rb = _go.GetComponent<Rigidbody2D>();
            Assert.AreEqual(RigidbodyType2D.Kinematic, rb.bodyType);
            Assert.AreEqual(0.5f, rb.gravityScale, 0.001f);
        }

        [Test]
        public void AddRigidbody_FreezeRotation_SetsConstraint()
        {
            _go = new GameObject("RB2DFreeze");

            var result = Physics2DAddRigidbodyTool.Execute(new Physics2DAddRigidbodyParams
            {
                Name = "RB2DFreeze", FreezeRotation = true
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.FreezeRotation);
            Assert.AreEqual(RigidbodyConstraints2D.FreezeRotation, _go.GetComponent<Rigidbody2D>().constraints);
        }

        [Test]
        public void AddRigidbody_ContinuousCollisionDetection_IsApplied()
        {
            _go = new GameObject("RB2DContinuous");

            var result = Physics2DAddRigidbodyTool.Execute(new Physics2DAddRigidbodyParams
            {
                Name = "RB2DContinuous", CollisionDetectionMode = "Continuous"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(CollisionDetectionMode2D.Continuous, _go.GetComponent<Rigidbody2D>().collisionDetectionMode);
        }

        [Test]
        public void AddRigidbody_UnknownBodyType_Fails()
        {
            _go = new GameObject("RB2DBad");

            var result = Physics2DAddRigidbodyTool.Execute(new Physics2DAddRigidbodyParams
            {
                Name = "RB2DBad", BodyType = "NotAType"
            });

            Assert.IsFalse(result.Success);
        }

        // ── physics2d/add-collider ───────────────────────────────────────────

        [Test]
        public void AddCollider_Box_AutoFitsToSpriteBoundsWhenSizeOmitted()
        {
            _go = new GameObject("Collider2DBox");
            var sr = _go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TestSpritePath);

            var result = Physics2DAddColliderTool.Execute(new Physics2DAddColliderParams
            {
                Name = "Collider2DBox", ColliderType = "Box"
            });

            Assert.IsTrue(result.Success, result.Error);
            var box = _go.GetComponent<BoxCollider2D>();
            Assert.IsNotNull(box);
            Assert.AreEqual(1f, box.size.x, 0.01f, "8px / 8 PPU");
            Assert.AreEqual(0.5f, box.size.y, 0.01f, "4px / 8 PPU");
        }

        [Test]
        public void AddCollider_Box_ExplicitSizeOverridesAutoFit()
        {
            _go = new GameObject("Collider2DBoxExplicit");
            var sr = _go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(TestSpritePath);

            var result = Physics2DAddColliderTool.Execute(new Physics2DAddColliderParams
            {
                Name = "Collider2DBoxExplicit", ColliderType = "Box", Size = new[] { 3f, 3f }
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(new Vector2(3f, 3f), _go.GetComponent<BoxCollider2D>().size);
        }

        [Test]
        public void AddCollider_Edge_SetsPoints()
        {
            _go = new GameObject("Collider2DEdge");

            var result = Physics2DAddColliderTool.Execute(new Physics2DAddColliderParams
            {
                Name = "Collider2DEdge", ColliderType = "Edge",
                Points = new[] { new[] { 0f, 0f }, new[] { 1f, 0f }, new[] { 1f, 1f } },
            });

            Assert.IsTrue(result.Success, result.Error);
            var edge = _go.GetComponent<EdgeCollider2D>();
            Assert.AreEqual(3, edge.pointCount);
            Assert.AreEqual(new Vector2(1f, 1f), edge.points[2]);
        }

        [Test]
        public void AddCollider_Edge_MissingPoints_Fails()
        {
            _go = new GameObject("Collider2DEdgeNoPoints");

            var result = Physics2DAddColliderTool.Execute(new Physics2DAddColliderParams
            {
                Name = "Collider2DEdgeNoPoints", ColliderType = "Edge"
            });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void AddCollider_IsTriggerAndOffsetAreApplied()
        {
            _go = new GameObject("Collider2DTrigger");

            var result = Physics2DAddColliderTool.Execute(new Physics2DAddColliderParams
            {
                Name = "Collider2DTrigger", ColliderType = "Circle", Radius = 1f,
                IsTrigger = true, Offset = new[] { 0.5f, -0.5f },
            });

            Assert.IsTrue(result.Success, result.Error);
            var circle = _go.GetComponent<CircleCollider2D>();
            Assert.IsTrue(circle.isTrigger);
            Assert.AreEqual(new Vector2(0.5f, -0.5f), circle.offset);
        }

        [Test]
        public void AddCollider_AddRigidbodyTrue_AddsOneWhenMissing()
        {
            _go = new GameObject("Collider2DWithRb");

            var result = Physics2DAddColliderTool.Execute(new Physics2DAddColliderParams
            {
                Name = "Collider2DWithRb", ColliderType = "Circle", Radius = 0.5f, AddRigidbody = true
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.RigidbodyAdded);
            Assert.IsNotNull(_go.GetComponent<Rigidbody2D>());
        }

        [Test]
        public void AddCollider_UnknownType_Fails()
        {
            _go = new GameObject("Collider2DBadType");

            var result = Physics2DAddColliderTool.Execute(new Physics2DAddColliderParams
            {
                Name = "Collider2DBadType", ColliderType = "NotAType"
            });

            Assert.IsFalse(result.Success);
        }
    }
}

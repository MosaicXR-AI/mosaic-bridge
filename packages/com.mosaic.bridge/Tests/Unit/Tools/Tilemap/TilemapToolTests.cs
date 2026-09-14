using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using Mosaic.Bridge.Tools.Tilemap;

namespace Mosaic.Bridge.Tests.Unit.Tools.Tilemap
{
    // O4 §4.1 (G1): zero 2D routes existed before this. tilemap/set-tiles in particular carries the
    // §3.5 hazard the field report is built around — a route that reports success from the request
    // instead of a read-back can silently paint nothing. These tests use real GameObjects, real
    // Tile assets, and a real Tilemap, and check PaintedCount against Tilemap.GetTile directly.
    [TestFixture]
    [Category("Tilemap")]
    public class TilemapToolTests
    {
        private const string TestSpritePath = "Assets/MosaicBridgeTests_TilemapSprite.png";
        private const string TestTileAssetPath = "Assets/MosaicBridgeTests_TilemapTile.asset";
        private GameObject _grid;

        [SetUp]
        public void SetUp()
        {
            var tex = new Texture2D(4, 4);
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(0, 255, 0, 255);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(TestSpritePath, ImageConversion.EncodeToPNG(tex));
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(TestSpritePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(TestSpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }

        [TearDown]
        public void TearDown()
        {
            if (_grid != null)
            {
                Object.DestroyImmediate(_grid);
                _grid = null;
            }
            AssetDatabase.DeleteAsset(TestSpritePath);
            AssetDatabase.DeleteAsset(TestTileAssetPath);
        }

        // ── tilemap/create ───────────────────────────────────────────────────

        [Test]
        public void Create_MakesGridAndTilemapLayerWithRendererComponents()
        {
            var result = TilemapCreateTool.Execute(new TilemapCreateParams { Name = "Ground" });

            Assert.IsTrue(result.Success, result.Error);
            _grid = GameObject.Find(result.Data.GridName);
            Assert.IsNotNull(_grid);
            Assert.IsNotNull(_grid.GetComponent<Grid>());
            var layer = GameObject.Find("Ground");
            Assert.IsNotNull(layer);
            Assert.IsNotNull(layer.GetComponent<UnityEngine.Tilemaps.Tilemap>());
            Assert.IsNotNull(layer.GetComponent<TilemapRenderer>());
            Assert.AreSame(_grid.transform, layer.transform.parent);
        }

        [Test]
        public void Create_SecondLayer_ReusesExistingGrid()
        {
            var first = TilemapCreateTool.Execute(new TilemapCreateParams { Name = "Ground" });
            _grid = GameObject.Find(first.Data.GridName);

            var second = TilemapCreateTool.Execute(new TilemapCreateParams
            {
                Name = "Background", GridName = first.Data.GridName
            });

            Assert.IsTrue(second.Success, second.Error);
            Assert.AreEqual(first.Data.GridInstanceId, second.Data.GridInstanceId,
                "a second layer must attach to the SAME Grid, not create its own");
            var backgroundLayer = GameObject.Find("Background");
            Assert.AreSame(_grid.transform, backgroundLayer.transform.parent);
        }

        [Test]
        public void Create_UnknownCellLayout_Fails()
        {
            var result = TilemapCreateTool.Execute(new TilemapCreateParams { CellLayout = "NotALayout" });

            Assert.IsFalse(result.Success);
        }

        // ── tilemap/create-tile ──────────────────────────────────────────────

        [Test]
        public void CreateTile_SingleMode_CreatesAssetWithSpriteAndColliderType()
        {
            var result = TilemapCreateTileTool.Execute(new TilemapCreateTileParams
            {
                SpritePath = TestSpritePath, AssetPath = TestTileAssetPath, ColliderType = "Grid"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.Tiles.Length);
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(TestTileAssetPath);
            Assert.IsNotNull(tile);
            Assert.IsNotNull(tile.sprite);
            Assert.AreEqual(Tile.ColliderType.Grid, tile.colliderType);
        }

        [Test]
        public void CreateTile_BothModesProvided_Fails()
        {
            var result = TilemapCreateTileTool.Execute(new TilemapCreateTileParams
            {
                SpritePath = TestSpritePath, AssetPath = TestTileAssetPath,
                SpriteSheetPath = TestSpritePath, OutputFolder = "Assets"
            });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void CreateTile_InvalidColliderType_Fails()
        {
            var result = TilemapCreateTileTool.Execute(new TilemapCreateTileParams
            {
                SpritePath = TestSpritePath, AssetPath = TestTileAssetPath, ColliderType = "NotAType"
            });

            Assert.IsFalse(result.Success);
        }

        // ── tilemap/set-tiles + tilemap/info ─────────────────────────────────

        [Test]
        public void SetTiles_AsciiMode_PaintsAndReportsAccurateReadBackCount()
        {
            TilemapCreateTileTool.Execute(new TilemapCreateTileParams
            {
                SpritePath = TestSpritePath, AssetPath = TestTileAssetPath, ColliderType = "None"
            });
            var created = TilemapCreateTool.Execute(new TilemapCreateParams { Name = "PaintTarget" });
            _grid = GameObject.Find(created.Data.GridName);

            var result = TilemapSetTilesTool.Execute(new TilemapSetTilesParams
            {
                TilemapName = "PaintTarget",
                AsciiMap = new[] { "X.X", ".X." },
                Legend = new System.Collections.Generic.Dictionary<string, string> { ["X"] = TestTileAssetPath },
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(3, result.Data.CellsRequested);
            Assert.AreEqual(3, result.Data.PaintedCount);

            var tilemap = GameObject.Find("PaintTarget").GetComponent<UnityEngine.Tilemaps.Tilemap>();
            // Row 0 ("X.X") is the TOP row -> highest Y (map has 2 rows, so y=1); row 1 -> y=0.
            Assert.IsNotNull(tilemap.GetTile(new Vector3Int(0, 1, 0)));
            Assert.IsNull(tilemap.GetTile(new Vector3Int(1, 1, 0)));
            Assert.IsNotNull(tilemap.GetTile(new Vector3Int(2, 1, 0)));
            Assert.IsNotNull(tilemap.GetTile(new Vector3Int(1, 0, 0)));
        }

        [Test]
        public void SetTiles_UnknownLegendCharacter_Fails()
        {
            var created = TilemapCreateTool.Execute(new TilemapCreateParams { Name = "PaintTarget2" });
            _grid = GameObject.Find(created.Data.GridName);

            var result = TilemapSetTilesTool.Execute(new TilemapSetTilesParams
            {
                TilemapName = "PaintTarget2",
                AsciiMap = new[] { "Q" },
                Legend = new System.Collections.Generic.Dictionary<string, string>(),
            });

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void SetTiles_CellsMode_PaintsExplicitCells()
        {
            TilemapCreateTileTool.Execute(new TilemapCreateTileParams
            {
                SpritePath = TestSpritePath, AssetPath = TestTileAssetPath
            });
            var created = TilemapCreateTool.Execute(new TilemapCreateParams { Name = "PaintTarget3" });
            _grid = GameObject.Find(created.Data.GridName);

            var result = TilemapSetTilesTool.Execute(new TilemapSetTilesParams
            {
                TilemapName = "PaintTarget3",
                Cells = new[] { new TileCellParam { X = 5, Y = -2, TileAssetPath = TestTileAssetPath } },
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.PaintedCount);
            var tilemap = GameObject.Find("PaintTarget3").GetComponent<UnityEngine.Tilemaps.Tilemap>();
            Assert.IsNotNull(tilemap.GetTile(new Vector3Int(5, -2, 0)));
        }

        [Test]
        public void Info_OccupiedCellCount_CountsCellsNotDistinctTileAssets()
        {
            // The regression this test exists for: GetUsedTilesCount() counts distinct Tile
            // ASSETS, not painted cells — two cells sharing one Tile asset must still report an
            // OccupiedCellCount of 2, with DistinctTileAssetCount reporting 1 alongside it.
            TilemapCreateTileTool.Execute(new TilemapCreateTileParams
            {
                SpritePath = TestSpritePath, AssetPath = TestTileAssetPath
            });
            var created = TilemapCreateTool.Execute(new TilemapCreateParams { Name = "InfoTarget" });
            _grid = GameObject.Find(created.Data.GridName);
            TilemapSetTilesTool.Execute(new TilemapSetTilesParams
            {
                TilemapName = "InfoTarget",
                Cells = new[]
                {
                    new TileCellParam { X = 0, Y = 0, TileAssetPath = TestTileAssetPath },
                    new TileCellParam { X = 1, Y = 0, TileAssetPath = TestTileAssetPath },
                },
            });

            var info = TilemapInfoTool.Execute(new TilemapInfoParams { TilemapName = "InfoTarget" });

            Assert.IsTrue(info.Success, info.Error);
            Assert.AreEqual(2, info.Data.OccupiedCellCount);
            Assert.AreEqual(1, info.Data.DistinctTileAssetCount);
            Assert.AreEqual("Rectangle", info.Data.GridCellLayout);
        }

        // ── tilemap/add-collider ─────────────────────────────────────────────

        [Test]
        public void AddCollider_Composite_AddsStaticRigidbodyTilemapAndCompositeCollider()
        {
            var created = TilemapCreateTool.Execute(new TilemapCreateParams { Name = "ColliderTarget" });
            _grid = GameObject.Find(created.Data.GridName);

            var result = TilemapAddColliderTool.Execute(new TilemapAddColliderParams
            {
                TilemapName = "ColliderTarget", Composite = true
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.RigidbodyAdded);
            Assert.IsTrue(result.Data.TilemapColliderAdded);
            Assert.IsTrue(result.Data.CompositeColliderAdded);

            var go = GameObject.Find("ColliderTarget");
            var rb = go.GetComponent<Rigidbody2D>();
            Assert.IsNotNull(rb);
            Assert.AreEqual(RigidbodyType2D.Static, rb.bodyType,
                "Rigidbody2D must be Static — a non-static body here produces empty composite geometry");
            Assert.IsNotNull(go.GetComponent<CompositeCollider2D>());
        }

        [Test]
        public void AddCollider_UsedByEffector_AddsEffectorAndSetsFlagOnComposite()
        {
            var created = TilemapCreateTool.Execute(new TilemapCreateParams { Name = "EffectorTarget" });
            _grid = GameObject.Find(created.Data.GridName);

            var result = TilemapAddColliderTool.Execute(new TilemapAddColliderParams
            {
                TilemapName = "EffectorTarget", Composite = true, UsedByEffector = true
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.EffectorAdded);
            var go = GameObject.Find("EffectorTarget");
            Assert.IsNotNull(go.GetComponent<PlatformEffector2D>());
            Assert.IsTrue(go.GetComponent<CompositeCollider2D>().usedByEffector,
                "an effector with usedByEffector left false is a classic silent no-op");
        }
    }
}

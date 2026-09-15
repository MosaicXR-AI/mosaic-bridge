using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Terrains;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tests.Terrains
{
    /// <summary>
    /// L14: terrain/paint add-layer used to mint a brand-new .terrainlayer asset on every call,
    /// named after the calling terrain — so a course's terrain/grid (many adjacent Terrain tiles)
    /// ended up with one duplicate layer asset per tile instead of one shared layer, and painting
    /// one tile never matched its neighbors.
    /// </summary>
    [TestFixture]
    [Category("Terrain")]
    public class TerrainPaintToolTests
    {
        private GameObject _terrainA;
        private GameObject _terrainB;
        private string _terrainDataPathA;
        private string _terrainDataPathB;
        private const string TexturePath = "Assets/MosaicTerrainPaintTestTexture.png";
        private const string LayerPath = "Assets/TerrainData/MosaicTerrainPaintTestTexture.terrainlayer";

        [SetUp]
        public void SetUp()
        {
            var a = TerrainCreateTool.Execute(new TerrainCreateParams
            {
                Name = "TestTerrain_PaintA", Width = 50f, Length = 50f, Height = 20f, HeightmapResolution = 33
            });
            Assert.IsTrue(a.Success, a.Error);
            _terrainA = UnityIds.Resolve(a.Data.InstanceId) as GameObject;
            _terrainDataPathA = a.Data.TerrainDataAssetPath;

            var b = TerrainCreateTool.Execute(new TerrainCreateParams
            {
                Name = "TestTerrain_PaintB", Width = 50f, Length = 50f, Height = 20f, HeightmapResolution = 33,
                Position = new[] { 50f, 0f, 0f }
            });
            Assert.IsTrue(b.Success, b.Error);
            _terrainB = UnityIds.Resolve(b.Data.InstanceId) as GameObject;
            _terrainDataPathB = b.Data.TerrainDataAssetPath;

            var tex = new Texture2D(4, 4);
            System.IO.File.WriteAllBytes(
                System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), TexturePath),
                ImageConversion.EncodeToPNG(tex));
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceSynchronousImport);
        }

        [TearDown]
        public void TearDown()
        {
            if (_terrainA != null) Object.DestroyImmediate(_terrainA);
            if (_terrainB != null) Object.DestroyImmediate(_terrainB);
            if (!string.IsNullOrEmpty(_terrainDataPathA) && AssetDatabase.AssetPathExists(_terrainDataPathA))
                AssetDatabase.DeleteAsset(_terrainDataPathA);
            if (!string.IsNullOrEmpty(_terrainDataPathB) && AssetDatabase.AssetPathExists(_terrainDataPathB))
                AssetDatabase.DeleteAsset(_terrainDataPathB);
            if (AssetDatabase.AssetPathExists(TexturePath)) AssetDatabase.DeleteAsset(TexturePath);
            if (AssetDatabase.AssetPathExists(LayerPath)) AssetDatabase.DeleteAsset(LayerPath);
        }

        [Test]
        public void AddLayer_SameTextureOnTwoTerrains_SharesOneLayerAsset()
        {
            var first = TerrainPaintTool.Execute(new TerrainPaintParams
            {
                Name = "TestTerrain_PaintA", Action = "add-layer", TexturePath = TexturePath
            });
            Assert.IsTrue(first.Success, first.Error);

            var second = TerrainPaintTool.Execute(new TerrainPaintParams
            {
                Name = "TestTerrain_PaintB", Action = "add-layer", TexturePath = TexturePath
            });
            Assert.IsTrue(second.Success, second.Error);

            var terrainA = _terrainA.GetComponent<Terrain>();
            var terrainB = _terrainB.GetComponent<Terrain>();
            Assert.AreEqual(1, terrainA.terrainData.terrainLayers.Length);
            Assert.AreEqual(1, terrainB.terrainData.terrainLayers.Length);
            Assert.AreSame(terrainA.terrainData.terrainLayers[0], terrainB.terrainData.terrainLayers[0],
                "both terrains must reference the SAME TerrainLayer asset, not two duplicates");

            var onDisk = new System.Collections.Generic.List<string>(
                AssetDatabase.FindAssets("t:TerrainLayer", new[] { "Assets/TerrainData" }));
            Assert.AreEqual(1, onDisk.Count, "exactly one .terrainlayer asset should exist for this texture");
        }

        [Test]
        public void AddLayer_CalledTwiceOnSameTerrain_IsIdempotent()
        {
            TerrainPaintTool.Execute(new TerrainPaintParams
            {
                Name = "TestTerrain_PaintA", Action = "add-layer", TexturePath = TexturePath
            });
            var second = TerrainPaintTool.Execute(new TerrainPaintParams
            {
                Name = "TestTerrain_PaintA", Action = "add-layer", TexturePath = TexturePath
            });

            Assert.IsTrue(second.Success, second.Error);
            Assert.AreEqual(1, _terrainA.GetComponent<Terrain>().terrainData.terrainLayers.Length,
                "adding the same texture twice must not duplicate the layer on the terrain");
            Assert.AreEqual(0, second.Data.LayerIndex);
        }

        // O4 §4.3: PBR layer authoring (maskMap, metallic, smoothness, tileOffset, normalScale,
        // diffuse remap) — previously only diffuseTexture/normalMapTexture/tileSize were settable.

        [Test]
        public void AddLayer_PbrFields_ApplyToTheLayerAsset()
        {
            var result = TerrainPaintTool.Execute(new TerrainPaintParams
            {
                Name = "TestTerrain_PaintA", Action = "add-layer", TexturePath = TexturePath,
                Metallic = 0.5f, Smoothness = 0.8f, NormalScale = 0.7f,
                TileOffset = new[] { 1f, 2f },
                DiffuseRemapMin = new[] { 0f, 0f, 0f, 0f },
                DiffuseRemapMax = new[] { 2f, 2f, 2f, 1f },
            });

            Assert.IsTrue(result.Success, result.Error);
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerPath);
            Assert.AreEqual(0.5f, layer.metallic, 0.0001f);
            Assert.AreEqual(0.8f, layer.smoothness, 0.0001f);
            Assert.AreEqual(0.7f, layer.normalScale, 0.0001f);
            Assert.AreEqual(new Vector2(1, 2), layer.tileOffset);
            Assert.AreEqual(new Vector4(2, 2, 2, 1), layer.diffuseRemapMax);
        }

        [Test]
        public void AddLayer_PbrFields_ApplyOnReusedLayerToo()
        {
            TerrainPaintTool.Execute(new TerrainPaintParams
            {
                Name = "TestTerrain_PaintA", Action = "add-layer", TexturePath = TexturePath,
            });

            var result = TerrainPaintTool.Execute(new TerrainPaintParams
            {
                Name = "TestTerrain_PaintB", Action = "add-layer", TexturePath = TexturePath,
                Metallic = 0.9f,
            });

            Assert.IsTrue(result.Success, result.Error);
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerPath);
            Assert.AreEqual(0.9f, layer.metallic, 0.0001f);
        }

        [Test]
        public void AddLayer_InvalidTileOffset_ReturnsInvalidParam()
        {
            var result = TerrainPaintTool.Execute(new TerrainPaintParams
            {
                Name = "TestTerrain_PaintA", Action = "add-layer", TexturePath = TexturePath,
                TileOffset = new[] { 1f },
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        // O4 §4.3: batch/auto splatmap — procedural texturing without brush-call storms.

        [Test]
        public void Array_AppliesWeightsAndRedistributesOtherLayers()
        {
            TerrainPaintTool.Execute(new TerrainPaintParams
            {
                Name = "TestTerrain_PaintA", Action = "add-layer", TexturePath = TexturePath,
            });
            TerrainPaintTool.Execute(new TerrainPaintParams
            {
                Name = "TestTerrain_PaintA", Action = "fill-layer", LayerIndex = 0,
            });
            TerrainPaintTool.Execute(new TerrainPaintParams
            {
                Name = "TestTerrain_PaintA", Action = "add-layer",
                TexturePath = TexturePath, LayerAssetPath = "Assets/TerrainData/SecondLayer.terrainlayer",
            });

            var result = TerrainPaintTool.Execute(new TerrainPaintParams
            {
                Name = "TestTerrain_PaintA", Action = "array", LayerIndex = 1,
                ArrayX = 0, ArrayY = 0, Width = 2, HeightCells = 2,
                Weights = new[] { 1f, 1f, 1f, 1f },
            });

            Assert.IsTrue(result.Success, result.Error);
            var terrain = _terrainA.GetComponent<Terrain>();
            var alphas = terrain.terrainData.GetAlphamaps(0, 0, 2, 2);
            for (int y = 0; y < 2; y++)
            for (int x = 0; x < 2; x++)
            {
                Assert.AreEqual(1f, alphas[y, x, 1], 0.0001f);
                Assert.AreEqual(0f, alphas[y, x, 0], 0.0001f);
            }

            if (AssetDatabase.AssetPathExists("Assets/TerrainData/SecondLayer.terrainlayer"))
                AssetDatabase.DeleteAsset("Assets/TerrainData/SecondLayer.terrainlayer");
        }

        [Test]
        public void Array_MismatchedWeightsLength_ReturnsInvalidParam()
        {
            TerrainPaintTool.Execute(new TerrainPaintParams
            {
                Name = "TestTerrain_PaintA", Action = "add-layer", TexturePath = TexturePath,
            });

            var result = TerrainPaintTool.Execute(new TerrainPaintParams
            {
                Name = "TestTerrain_PaintA", Action = "array", LayerIndex = 0,
                Width = 2, HeightCells = 2, Weights = new[] { 1f },
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Auto_ByHeight_PaintsOnlyMatchingSamples()
        {
            TerrainPaintTool.Execute(new TerrainPaintParams
            {
                Name = "TestTerrain_PaintA", Action = "add-layer", TexturePath = TexturePath,
            });
            TerrainPaintTool.Execute(new TerrainPaintParams
            {
                Name = "TestTerrain_PaintA", Action = "add-layer",
                TexturePath = TexturePath, LayerAssetPath = "Assets/TerrainData/SecondLayer2.terrainlayer",
            });

            // Raise the whole terrain to a known height, then only the second layer should match
            // a MinHeight above it (nothing should paint).
            var terrain = _terrainA.GetComponent<Terrain>();
            var res = terrain.terrainData.heightmapResolution;
            var heights = new float[res, res];
            for (int y = 0; y < res; y++)
                for (int x = 0; x < res; x++)
                    heights[y, x] = 0.1f; // low
            terrain.terrainData.SetHeights(0, 0, heights);

            var result = TerrainPaintTool.Execute(new TerrainPaintParams
            {
                Name = "TestTerrain_PaintA", Action = "auto", LayerIndex = 1, MinHeight = 1000f,
            });

            Assert.IsTrue(result.Success, result.Error);
            StringAssert.Contains("0 of", result.Data.Message);

            if (AssetDatabase.AssetPathExists("Assets/TerrainData/SecondLayer2.terrainlayer"))
                AssetDatabase.DeleteAsset("Assets/TerrainData/SecondLayer2.terrainlayer");
        }

        [Test]
        public void Auto_NoConstraints_ReturnsInvalidParam()
        {
            TerrainPaintTool.Execute(new TerrainPaintParams
            {
                Name = "TestTerrain_PaintA", Action = "add-layer", TexturePath = TexturePath,
            });

            var result = TerrainPaintTool.Execute(new TerrainPaintParams
            {
                Name = "TestTerrain_PaintA", Action = "auto", LayerIndex = 0,
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}

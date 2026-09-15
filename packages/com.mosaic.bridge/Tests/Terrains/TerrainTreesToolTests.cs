using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Terrains;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tests.Terrains
{
    // O4 §4.3: explicit tree lists + masked scatter — forests that avoid roads/water/cliffs.
    [TestFixture]
    [Category("Terrain")]
    public class TerrainTreesToolTests
    {
        private const string PrefabPath = "Assets/MosaicTestTreePrefab.prefab";
        private GameObject _terrainGo;
        private string _terrainDataPath;

        [SetUp]
        public void SetUp()
        {
            var created = TerrainCreateTool.Execute(new TerrainCreateParams
            {
                Name = "TestTerrain_Trees", Width = 50f, Length = 50f, Height = 20f, HeightmapResolution = 33,
            });
            Assert.IsTrue(created.Success, created.Error);
            _terrainGo = UnityIds.Resolve(created.Data.InstanceId) as GameObject;
            _terrainDataPath = created.Data.TerrainDataAssetPath;

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            PrefabUtility.SaveAsPrefabAsset(cube, PrefabPath);
            Object.DestroyImmediate(cube);

            TerrainTreesTool.Execute(new TerrainTreesParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "add-prototype", PrefabPath = PrefabPath,
            });
        }

        [TearDown]
        public void TearDown()
        {
            if (_terrainGo != null) Object.DestroyImmediate(_terrainGo);
            if (!string.IsNullOrEmpty(_terrainDataPath) && AssetDatabase.AssetPathExists(_terrainDataPath))
                AssetDatabase.DeleteAsset(_terrainDataPath);
            if (AssetDatabase.AssetPathExists(PrefabPath)) AssetDatabase.DeleteAsset(PrefabPath);
        }

        [Test]
        public void PlaceList_PlacesExactPositionsRotationsAndColors()
        {
            var result = TerrainTreesTool.Execute(new TerrainTreesParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "place-list", PrototypeIndex = 0,
                Positions = new[] { 0.25f, 0f, 0.25f, 0.75f, 0f, 0.75f },
                Rotations = new[] { 45f, 90f },
                Colors = new[] { 1f, 0f, 0f, 0f, 1f, 0f },
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(2, result.Data.PlacedCount);

            var data = _terrainGo.GetComponent<Terrain>().terrainData;
            Assert.AreEqual(2, data.treeInstances.Length);
            Assert.AreEqual(45f, data.treeInstances[0].rotation, 0.001f);
            Assert.AreEqual(0.25f, data.treeInstances[0].position.x, 0.001f);
            // TreeInstance.color is Color32 (0..255 byte range), not a normalized 0..1 Color.
            Assert.AreEqual(255, data.treeInstances[0].color.r);
        }

        [Test]
        public void PlaceList_MismatchedRotationsLength_ReturnsInvalidParam()
        {
            var result = TerrainTreesTool.Execute(new TerrainTreesParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "place-list", PrototypeIndex = 0,
                Positions = new[] { 0.25f, 0f, 0.25f, 0.75f, 0f, 0.75f },
                Rotations = new[] { 45f },
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Place_MaskedByHeight_OnlyPlacesWithinRange()
        {
            var terrain = _terrainGo.GetComponent<Terrain>();
            // Flatten the whole terrain to a known low height so MinHeight above it rejects everything.
            var res = terrain.terrainData.heightmapResolution;
            var heights = new float[res, res];
            terrain.terrainData.SetHeights(0, 0, heights);

            var result = TerrainTreesTool.Execute(new TerrainTreesParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "place", PrototypeIndex = 0,
                Count = 5, MinHeight = 1000f, MaxAttempts = 20,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0, result.Data.PlacedCount);
        }

        [Test]
        public void GetInstances_ReturnsPositions()
        {
            TerrainTreesTool.Execute(new TerrainTreesParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "place-list", PrototypeIndex = 0,
                Positions = new[] { 0.5f, 0f, 0.5f },
            });

            var result = TerrainTreesTool.Execute(new TerrainTreesParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "get-instances",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.Instances.Length);
            Assert.AreEqual(0.5f, result.Data.Instances[0].Position[0], 0.001f);
        }

        [Test]
        public void PlaceList_NoPrototypes_ReturnsNotPermitted()
        {
            var freshTerrain = TerrainCreateTool.Execute(new TerrainCreateParams
            {
                Name = "TestTerrain_TreesFresh", Width = 50f, Length = 50f, HeightmapResolution = 33,
            });
            var freshGo = UnityIds.Resolve(freshTerrain.Data.InstanceId) as GameObject;
            try
            {
                var result = TerrainTreesTool.Execute(new TerrainTreesParams
                {
                    InstanceId = UnityIds.Of(freshGo), Action = "place-list",
                    Positions = new[] { 0.5f, 0f, 0.5f },
                });

                Assert.IsFalse(result.Success);
                Assert.AreEqual("NOT_PERMITTED", result.ErrorCode);
            }
            finally
            {
                Object.DestroyImmediate(freshGo);
                if (AssetDatabase.AssetPathExists(freshTerrain.Data.TerrainDataAssetPath))
                    AssetDatabase.DeleteAsset(freshTerrain.Data.TerrainDataAssetPath);
            }
        }
    }
}

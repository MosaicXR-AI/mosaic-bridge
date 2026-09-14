using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Terrains;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tests.Terrains
{
    [TestFixture]
    [Category("Terrain")]
    public class TerrainHeightToolTests
    {
        private GameObject _createdGo;
        private string _createdAssetPath;

        [SetUp]
        public void SetUp()
        {
            var createResult = TerrainCreateTool.Execute(new TerrainCreateParams
            {
                Name = "TestTerrain_Height",
                Width = 100f,
                Length = 100f,
                Height = 50f,
                HeightmapResolution = 33
            });
            Assert.IsTrue(createResult.Success, createResult.Error);
            _createdGo = UnityIds.Resolve(createResult.Data.InstanceId) as GameObject;
            _createdAssetPath = createResult.Data.TerrainDataAssetPath;
        }

        [TearDown]
        public void TearDown()
        {
            if (_createdGo != null)
                Object.DestroyImmediate(_createdGo);
            if (!string.IsNullOrEmpty(_createdAssetPath) && AssetDatabase.AssetPathExists(_createdAssetPath))
                AssetDatabase.DeleteAsset(_createdAssetPath);
        }

        [Test]
        public void Flatten_SetsAllHeightsToTarget()
        {
            var result = TerrainHeightTool.Execute(new TerrainHeightParams
            {
                Name = "TestTerrain_Height",
                Action = "flatten",
                Height = 0.5f
            });
            Assert.IsTrue(result.Success, result.Error);

            var terrain = _createdGo.GetComponent<UnityEngine.Terrain>();
            var heights = terrain.terrainData.GetHeights(0, 0, 1, 1);
            Assert.AreEqual(0.5f, heights[0, 0], 0.01f);
        }

        [Test]
        public void InvalidAction_ReturnsFail()
        {
            var result = TerrainHeightTool.Execute(new TerrainHeightParams
            {
                Name = "TestTerrain_Height",
                Action = "invalid_action"
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        // L13: SetHeightsDelayLOD requires an explicit TerrainData.SyncHeightmap() call afterward
        // — without it, a DelayLod batch's final non-delayed call could still leave a stale
        // TerrainCollider even though the visible heightmap looked correct. Verified end-to-end:
        // raise the terrain via a DelayLod write, flush with a non-delayed call, then raycast
        // against the actual TerrainCollider and confirm it reports the new height.
        [Test]
        public void Array_WithDelayLod_ThenFlush_UpdatesColliderHeight()
        {
            var terrain = _createdGo.GetComponent<UnityEngine.Terrain>();
            int res = terrain.terrainData.heightmapResolution;
            var heights = new float[res * res];
            for (int i = 0; i < heights.Length; i++) heights[i] = 0.8f;

            var delayed = TerrainHeightTool.Execute(new TerrainHeightParams
            {
                Name = "TestTerrain_Height", Action = "array",
                Heights = heights, Width = res, HeightCells = res,
                ArrayX = 0, ArrayY = 0, DelayLod = true
            });
            Assert.IsTrue(delayed.Success, delayed.Error);

            var flush = TerrainHeightTool.Execute(new TerrainHeightParams
            {
                Name = "TestTerrain_Height", Action = "array",
                Heights = heights, Width = res, HeightCells = res,
                ArrayX = 0, ArrayY = 0, DelayLod = false
            });
            Assert.IsTrue(flush.Success, flush.Error);

            UnityEngine.Physics.SyncTransforms();
            Assert.IsNotNull(_createdGo.GetComponent<TerrainCollider>());
            bool hit = UnityEngine.Physics.Raycast(
                new Vector3(50f, 200f, 50f), Vector3.down, out var hitInfo, 500f);
            Assert.IsTrue(hit, "raycast against the terrain collider must hit after SyncHeightmap+Flush");
            Assert.Greater(hitInfo.point.y, 30f,
                "collider height must reflect the raised heightmap (~40), not a stale default (~0)");
        }
    }
}

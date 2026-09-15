using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Terrains;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tests.Terrains
{
    // O4 §4.3: stitching individually created/imported terrains needs SetNeighbors so LOD
    // transitions align across tile boundaries.
    [TestFixture]
    [Category("Terrain")]
    public class TerrainNeighborsToolTests
    {
        private GameObject _center, _left, _right;
        private string _centerDataPath, _leftDataPath, _rightDataPath;

        [SetUp]
        public void SetUp()
        {
            var center = TerrainCreateTool.Execute(new TerrainCreateParams
            {
                Name = "TestTerrain_NeighborsCenter", Width = 50f, Length = 50f, HeightmapResolution = 33,
            });
            _center = UnityIds.Resolve(center.Data.InstanceId) as GameObject;
            _centerDataPath = center.Data.TerrainDataAssetPath;

            var left = TerrainCreateTool.Execute(new TerrainCreateParams
            {
                Name = "TestTerrain_NeighborsLeft", Width = 50f, Length = 50f, HeightmapResolution = 33,
                Position = new[] { -50f, 0f, 0f },
            });
            _left = UnityIds.Resolve(left.Data.InstanceId) as GameObject;
            _leftDataPath = left.Data.TerrainDataAssetPath;

            var right = TerrainCreateTool.Execute(new TerrainCreateParams
            {
                Name = "TestTerrain_NeighborsRight", Width = 50f, Length = 50f, HeightmapResolution = 33,
                Position = new[] { 50f, 0f, 0f },
            });
            _right = UnityIds.Resolve(right.Data.InstanceId) as GameObject;
            _rightDataPath = right.Data.TerrainDataAssetPath;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in new[] { _center, _left, _right })
                if (go != null) Object.DestroyImmediate(go);
            foreach (var path in new[] { _centerDataPath, _leftDataPath, _rightDataPath })
                if (!string.IsNullOrEmpty(path) && AssetDatabase.AssetPathExists(path))
                    AssetDatabase.DeleteAsset(path);
        }

        [Test]
        public void SetNeighbors_ByName_ConnectsTiles()
        {
            var result = TerrainNeighborsTool.Execute(new TerrainNeighborsParams
            {
                Name = "TestTerrain_NeighborsCenter",
                LeftName = "TestTerrain_NeighborsLeft", RightName = "TestTerrain_NeighborsRight",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("TestTerrain_NeighborsLeft", result.Data.Left);
            Assert.AreEqual("TestTerrain_NeighborsRight", result.Data.Right);
            Assert.IsNull(result.Data.Top);
            Assert.IsNull(result.Data.Bottom);

            var centerTerrain = _center.GetComponent<Terrain>();
            Assert.AreEqual(_left.GetComponent<Terrain>(), centerTerrain.leftNeighbor);
            Assert.AreEqual(_right.GetComponent<Terrain>(), centerTerrain.rightNeighbor);
        }

        [Test]
        public void SetNeighbors_UnknownName_ReturnsNotFound()
        {
            var result = TerrainNeighborsTool.Execute(new TerrainNeighborsParams
            {
                Name = "TestTerrain_NeighborsCenter", LeftName = "NoSuchTerrain",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void Settings_AllowAutoConnectAndGroupingId_Apply()
        {
            var result = TerrainSettingsTool.Execute(new TerrainSettingsParams
            {
                Name = "TestTerrain_NeighborsCenter", AllowAutoConnect = true, GroupingId = 7,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.AllowAutoConnect);
            Assert.AreEqual(7, result.Data.GroupingId);
            var terrain = _center.GetComponent<Terrain>();
            Assert.IsTrue(terrain.allowAutoConnect);
            Assert.AreEqual(7, terrain.groupingID);
        }
    }
}

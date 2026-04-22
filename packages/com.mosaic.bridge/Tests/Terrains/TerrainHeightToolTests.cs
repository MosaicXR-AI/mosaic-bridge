using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Terrains;

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
            _createdGo = Resources.EntityIdToObject(createResult.Data.InstanceId) as GameObject;
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
    }
}

using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Terrains;

namespace Mosaic.Bridge.Tests.Terrains
{
    [TestFixture]
    [Category("Terrain")]
    public class TerrainSampleHeightToolTests
    {
        private GameObject _terrainGo;
        private string _assetPath;

        [SetUp]
        public void SetUp()
        {
            var r = TerrainCreateTool.Execute(new TerrainCreateParams
            {
                Name = "TestTerrain_SampleHeight",
                Width = 500f,
                Length = 500f,
                Height = 200f,
                HeightmapResolution = 129
            });
            Assert.IsTrue(r.Success, r.Error);
            _terrainGo   = Resources.EntityIdToObject(r.Data.InstanceId) as GameObject;
            _assetPath   = r.Data.TerrainDataAssetPath;
        }

        [TearDown]
        public void TearDown()
        {
            if (_terrainGo != null)
                Object.DestroyImmediate(_terrainGo);
            if (!string.IsNullOrEmpty(_assetPath) && AssetDatabase.AssetPathExists(_assetPath))
                AssetDatabase.DeleteAsset(_assetPath);
        }

        [Test]
        public void SampleHeight_FlatTerrain_ReturnsZero()
        {
            var result = TerrainSampleHeightTool.Execute(new TerrainSampleHeightParams
            {
                WorldX = 250f,
                WorldZ = 250f,
                TerrainName = "TestTerrain_SampleHeight"
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0f, result.Data.WorldY, 0.01f, "Flat terrain should return Y=0");
            Assert.AreEqual(0f, result.Data.NormalizedHeight, 0.001f);
        }

        [Test]
        public void SampleHeight_ReturnsTerrainName()
        {
            var result = TerrainSampleHeightTool.Execute(new TerrainSampleHeightParams
            {
                WorldX = 100f,
                WorldZ = 100f,
                TerrainName = "TestTerrain_SampleHeight"
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("TestTerrain_SampleHeight", result.Data.TerrainName);
        }

        [Test]
        public void SampleHeight_ReturnsSuggestedPlacementY_WithOffset()
        {
            var result = TerrainSampleHeightTool.Execute(new TerrainSampleHeightParams
            {
                WorldX = 100f,
                WorldZ = 100f,
                TerrainName = "TestTerrain_SampleHeight"
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data.SuggestedPlacementY);
            Assert.AreEqual(3, result.Data.SuggestedPlacementY.Length);
            Assert.AreEqual(result.Data.WorldY + 0.1f, result.Data.SuggestedPlacementY[1], 0.001f,
                "SuggestedPlacementY[1] should be WorldY + 0.1 offset");
        }

        [Test]
        public void SampleHeight_ByInstanceId_Works()
        {
            int id = _terrainGo.GetInstanceID();
            var result = TerrainSampleHeightTool.Execute(new TerrainSampleHeightParams
            {
                WorldX = 50f,
                WorldZ = 50f,
                InstanceId = id
            });
            Assert.IsTrue(result.Success, result.Error);
        }

        [Test]
        public void SampleHeight_NotFound_ReturnsFail()
        {
            var result = TerrainSampleHeightTool.Execute(new TerrainSampleHeightParams
            {
                WorldX = 0f,
                WorldZ = 0f,
                TerrainName = "NonExistentTerrain_XYZ"
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }
    }
}

using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Terrains;

namespace Mosaic.Bridge.Tests.Terrains
{
    [TestFixture]
    [Category("Terrain")]
    public class TerrainGetRegionsToolTests
    {
        private GameObject _terrainGo;
        private string _assetPath;

        [SetUp]
        public void SetUp()
        {
            var r = TerrainCreateTool.Execute(new TerrainCreateParams
            {
                Name = "TestTerrain_GetRegions",
                Width = 500f, Length = 500f, Height = 200f,
                HeightmapResolution = 129
            });
            Assert.IsTrue(r.Success, r.Error);
            _terrainGo = Resources.EntityIdToObject(r.Data.InstanceId) as GameObject;
            _assetPath = r.Data.TerrainDataAssetPath;
        }

        [TearDown]
        public void TearDown()
        {
            if (_terrainGo != null) Object.DestroyImmediate(_terrainGo);
            if (!string.IsNullOrEmpty(_assetPath) && AssetDatabase.AssetPathExists(_assetPath))
                AssetDatabase.DeleteAsset(_assetPath);
        }

        [Test]
        public void GetRegions_NoLayers_ReturnsEmptyRegions()
        {
            var result = TerrainGetRegionsTool.Execute(new TerrainGetRegionsParams
            {
                TerrainName = "TestTerrain_GetRegions"
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0, result.Data.LayerCount);
            Assert.AreEqual(0, result.Data.Regions.Length);
        }

        [Test]
        public void GetRegions_ReturnsTerrainName()
        {
            var result = TerrainGetRegionsTool.Execute(new TerrainGetRegionsParams
            {
                TerrainName = "TestTerrain_GetRegions"
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("TestTerrain_GetRegions", result.Data.TerrainName);
        }

        [Test]
        public void GetRegions_ByInstanceId_Works()
        {
            int id = _terrainGo.GetInstanceID();
            var result = TerrainGetRegionsTool.Execute(new TerrainGetRegionsParams
            {
                InstanceId = id
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("TestTerrain_GetRegions", result.Data.TerrainName);
        }

        [Test]
        public void GetRegions_NotFound_ReturnsFail()
        {
            var result = TerrainGetRegionsTool.Execute(new TerrainGetRegionsParams
            {
                TerrainName = "NonExistentTerrain_XYZ"
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }
    }
}

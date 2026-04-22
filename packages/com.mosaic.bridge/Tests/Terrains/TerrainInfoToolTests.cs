using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Terrains;

namespace Mosaic.Bridge.Tests.Terrains
{
    [TestFixture]
    [Category("Terrain")]
    public class TerrainInfoToolTests
    {
        private GameObject _createdGo;
        private string _createdAssetPath;

        [TearDown]
        public void TearDown()
        {
            if (_createdGo != null)
                Object.DestroyImmediate(_createdGo);

            if (!string.IsNullOrEmpty(_createdAssetPath) && AssetDatabase.AssetPathExists(_createdAssetPath))
                AssetDatabase.DeleteAsset(_createdAssetPath);
        }

        [Test]
        public void Info_ReturnsCorrectProperties()
        {
            // Create terrain first
            var createResult = TerrainCreateTool.Execute(new TerrainCreateParams
            {
                Name = "TestTerrain_Info",
                Width = 150f,
                Length = 200f,
                Height = 75f,
                HeightmapResolution = 129
            });
            Assert.IsTrue(createResult.Success, createResult.Error);

            _createdGo = Resources.EntityIdToObject(createResult.Data.InstanceId) as GameObject;
            _createdAssetPath = createResult.Data.TerrainDataAssetPath;

            // Query info
            var infoResult = TerrainInfoTool.Execute(new TerrainInfoParams
            {
                Name = "TestTerrain_Info"
            });
            Assert.IsTrue(infoResult.Success, infoResult.Error);

            Assert.AreEqual("TestTerrain_Info", infoResult.Data.Name);
            Assert.AreEqual(150f, infoResult.Data.Width, 0.01f);
            Assert.AreEqual(200f, infoResult.Data.Length, 0.01f);
            Assert.AreEqual(75f, infoResult.Data.Height, 0.01f);
            Assert.AreEqual(129, infoResult.Data.HeightmapResolution);
            Assert.AreEqual(0, infoResult.Data.LayerCount);
            Assert.AreEqual(0, infoResult.Data.TreePrototypeCount);
            Assert.AreEqual(0, infoResult.Data.TreeInstanceCount);
            Assert.AreEqual(0, infoResult.Data.DetailPrototypeCount);
        }

        [Test]
        public void Info_ByInstanceId_Works()
        {
            var createResult = TerrainCreateTool.Execute(new TerrainCreateParams
            {
                Name = "TestTerrain_InfoById",
                Width = 100f,
                Length = 100f,
                Height = 50f
            });
            Assert.IsTrue(createResult.Success, createResult.Error);

            _createdGo = Resources.EntityIdToObject(createResult.Data.InstanceId) as GameObject;
            _createdAssetPath = createResult.Data.TerrainDataAssetPath;

            var infoResult = TerrainInfoTool.Execute(new TerrainInfoParams
            {
                InstanceId = createResult.Data.InstanceId
            });
            Assert.IsTrue(infoResult.Success, infoResult.Error);
            Assert.AreEqual("TestTerrain_InfoById", infoResult.Data.Name);
        }

        [Test]
        public void Info_NotFound_ReturnsFail()
        {
            var result = TerrainInfoTool.Execute(new TerrainInfoParams
            {
                Name = "NonExistentTerrain_12345"
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }
    }
}

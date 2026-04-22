using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Terrains;

namespace Mosaic.Bridge.Tests.Terrains
{
    [TestFixture]
    [Category("Terrain")]
    public class TerrainCreateToolTests
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
        public void Create_DefaultParams_CreatesTerrain()
        {
            var p = new TerrainCreateParams
            {
                Name = "TestTerrain_Create",
                Width = 100f,
                Length = 100f,
                Height = 50f
            };

            var result = TerrainCreateTool.Execute(p);
            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data);

            _createdGo = Resources.EntityIdToObject(result.Data.InstanceId) as GameObject;
            _createdAssetPath = result.Data.TerrainDataAssetPath;

            Assert.IsNotNull(_createdGo);
            Assert.AreEqual("TestTerrain_Create", result.Data.Name);
            Assert.AreEqual(100f, result.Data.Width);
            Assert.AreEqual(100f, result.Data.Length);
            Assert.AreEqual(50f, result.Data.Height);
        }

        [Test]
        public void Create_VerifiesTerrainData()
        {
            var p = new TerrainCreateParams
            {
                Name = "TestTerrain_Data",
                Width = 200f,
                Length = 300f,
                Height = 100f,
                HeightmapResolution = 257
            };

            var result = TerrainCreateTool.Execute(p);
            Assert.IsTrue(result.Success, result.Error);

            _createdGo = Resources.EntityIdToObject(result.Data.InstanceId) as GameObject;
            _createdAssetPath = result.Data.TerrainDataAssetPath;

            var terrain = _createdGo.GetComponent<UnityEngine.Terrain>();
            Assert.IsNotNull(terrain);
            Assert.IsNotNull(terrain.terrainData);

            Assert.AreEqual(200f, terrain.terrainData.size.x, 0.01f);
            Assert.AreEqual(300f, terrain.terrainData.size.z, 0.01f);
            Assert.AreEqual(100f, terrain.terrainData.size.y, 0.01f);
            Assert.AreEqual(257, terrain.terrainData.heightmapResolution);
        }

        [Test]
        public void Create_SavesTerrainDataAsset()
        {
            var p = new TerrainCreateParams
            {
                Name = "TestTerrain_Asset",
                Width = 50f,
                Length = 50f,
                Height = 25f
            };

            var result = TerrainCreateTool.Execute(p);
            Assert.IsTrue(result.Success, result.Error);

            _createdGo = Resources.EntityIdToObject(result.Data.InstanceId) as GameObject;
            _createdAssetPath = result.Data.TerrainDataAssetPath;

            Assert.IsFalse(string.IsNullOrEmpty(_createdAssetPath));
            Assert.IsTrue(AssetDatabase.AssetPathExists(_createdAssetPath),
                $"TerrainData asset not found at '{_createdAssetPath}'");
        }

        [Test]
        public void Create_InvalidWidth_ReturnsFail()
        {
            var p = new TerrainCreateParams { Width = -1f };
            var result = TerrainCreateTool.Execute(p);
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}

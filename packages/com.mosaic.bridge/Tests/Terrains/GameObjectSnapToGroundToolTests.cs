using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Terrains;
using Mosaic.Bridge.Tools.GameObjects;

namespace Mosaic.Bridge.Tests.Terrains
{
    [TestFixture]
    [Category("Terrain")]
    public class GameObjectSnapToGroundToolTests
    {
        private GameObject _terrainGo;
        private string _assetPath;
        private GameObject _testObj;

        [SetUp]
        public void SetUp()
        {
            var r = TerrainCreateTool.Execute(new TerrainCreateParams
            {
                Name = "TestTerrain_SnapToGround",
                Width = 500f,
                Length = 500f,
                Height = 200f,
                HeightmapResolution = 129
            });
            Assert.IsTrue(r.Success, r.Error);
            _terrainGo = Resources.EntityIdToObject(r.Data.InstanceId) as GameObject;
            _assetPath = r.Data.TerrainDataAssetPath;

            _testObj = new GameObject("SnapTestObject");
        }

        [TearDown]
        public void TearDown()
        {
            if (_testObj != null)
                Object.DestroyImmediate(_testObj);
            if (_terrainGo != null)
                Object.DestroyImmediate(_terrainGo);
            if (!string.IsNullOrEmpty(_assetPath) && AssetDatabase.AssetPathExists(_assetPath))
                AssetDatabase.DeleteAsset(_assetPath);
        }

        [Test]
        public void SnapToGround_FlatTerrain_SetsCorrectY()
        {
            _testObj.transform.position = new Vector3(100f, 999f, 100f);

            var result = GameObjectSnapToGroundTool.Execute(new GameObjectSnapToGroundParams
            {
                GameObjectPath = "SnapTestObject",
                TerrainName    = "TestTerrain_SnapToGround",
                YOffset        = 0.05f
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0.05f, result.Data.NewY, 0.01f, "Flat terrain Y=0 + offset 0.05 = 0.05");
            Assert.AreEqual(999f, result.Data.PreviousY, 0.01f);
            Assert.AreEqual("terrain", result.Data.SnapMode);
        }

        [Test]
        public void SnapToGround_ByInstanceId_Works()
        {
            _testObj.transform.position = new Vector3(200f, 500f, 200f);
            int id = _testObj.GetInstanceID();

            var result = GameObjectSnapToGroundTool.Execute(new GameObjectSnapToGroundParams
            {
                InstanceId  = id,
                TerrainName = "TestTerrain_SnapToGround",
                YOffset     = 0f
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("SnapTestObject", result.Data.GameObjectName);
        }

        [Test]
        public void SnapToGround_NotFound_ReturnsFail()
        {
            var result = GameObjectSnapToGroundTool.Execute(new GameObjectSnapToGroundParams
            {
                GameObjectPath = "NonExistentObject_XYZ",
                TerrainName    = "TestTerrain_SnapToGround"
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void SnapToGround_MissingParams_ReturnsFail()
        {
            var result = GameObjectSnapToGroundTool.Execute(new GameObjectSnapToGroundParams
            {
                TerrainName = "TestTerrain_SnapToGround"
            });
            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}

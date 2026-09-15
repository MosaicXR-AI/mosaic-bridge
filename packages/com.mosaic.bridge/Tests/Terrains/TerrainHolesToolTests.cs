using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Terrains;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tests.Terrains
{
    // O4 §4.3: terrain/holes — cave/mine entrances. true = solid.
    [TestFixture]
    [Category("Terrain")]
    public class TerrainHolesToolTests
    {
        private GameObject _terrainGo;
        private string _terrainDataPath;

        [SetUp]
        public void SetUp()
        {
            var created = TerrainCreateTool.Execute(new TerrainCreateParams
            {
                Name = "TestTerrain_Holes", Width = 50f, Length = 50f, Height = 20f, HeightmapResolution = 33,
            });
            Assert.IsTrue(created.Success, created.Error);
            _terrainGo = UnityIds.Resolve(created.Data.InstanceId) as GameObject;
            _terrainDataPath = created.Data.TerrainDataAssetPath;
        }

        [TearDown]
        public void TearDown()
        {
            if (_terrainGo != null) Object.DestroyImmediate(_terrainGo);
            if (!string.IsNullOrEmpty(_terrainDataPath) && AssetDatabase.AssetPathExists(_terrainDataPath))
                AssetDatabase.DeleteAsset(_terrainDataPath);
        }

        [Test]
        public void Rect_PunchesAHole()
        {
            var result = TerrainHolesTool.Execute(new TerrainHolesParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "rect",
                RectX = 5, RectY = 5, RectWidth = 4, RectHeight = 4,
            });

            Assert.IsTrue(result.Success, result.Error);
            var data = _terrainGo.GetComponent<Terrain>().terrainData;
            Assert.IsFalse(data.GetHoles(6, 6, 1, 1)[0, 0], "inside the rect should be a hole");
            Assert.IsTrue(data.GetHoles(20, 20, 1, 1)[0, 0], "outside the rect should remain solid");
        }

        [Test]
        public void Circle_PunchesACircularHole()
        {
            var result = TerrainHolesTool.Execute(new TerrainHolesParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "circle",
                CenterX = 16, CenterY = 16, Radius = 3,
            });

            Assert.IsTrue(result.Success, result.Error);
            var data = _terrainGo.GetComponent<Terrain>().terrainData;
            Assert.IsFalse(data.GetHoles(16, 16, 1, 1)[0, 0], "center of the circle should be a hole");
            Assert.IsTrue(data.GetHoles(0, 0, 1, 1)[0, 0], "far corner should remain solid");
        }

        [Test]
        public void Array_And_Get_RoundTrip()
        {
            var setResult = TerrainHolesTool.Execute(new TerrainHolesParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "array",
                ArrayX = 0, ArrayY = 0, Width = 2, HeightCells = 2,
                Holes = new[] { false, true, true, false },
            });
            Assert.IsTrue(setResult.Success, setResult.Error);

            var getResult = TerrainHolesTool.Execute(new TerrainHolesParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "get",
                ArrayX = 0, ArrayY = 0, Width = 2, HeightCells = 2,
            });

            Assert.IsTrue(getResult.Success, getResult.Error);
            CollectionAssert.AreEqual(new[] { false, true, true, false }, getResult.Data.Holes);
        }

        [Test]
        public void Clear_MakesEverythingSolid()
        {
            TerrainHolesTool.Execute(new TerrainHolesParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "rect",
                RectX = 0, RectY = 0, RectWidth = 5, RectHeight = 5,
            });

            var result = TerrainHolesTool.Execute(new TerrainHolesParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "clear",
            });

            Assert.IsTrue(result.Success, result.Error);
            var data = _terrainGo.GetComponent<Terrain>().terrainData;
            Assert.IsTrue(data.GetHoles(2, 2, 1, 1)[0, 0]);
        }

        [Test]
        public void Rect_OutOfBounds_ReturnsInvalidParam()
        {
            var result = TerrainHolesTool.Execute(new TerrainHolesParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "rect",
                RectX = 30, RectY = 30, RectWidth = 10, RectHeight = 10,
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void Execute_UnknownAction_ReturnsInvalidParam()
        {
            var result = TerrainHolesTool.Execute(new TerrainHolesParams
            {
                InstanceId = UnityIds.Of(_terrainGo), Action = "bogus",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}

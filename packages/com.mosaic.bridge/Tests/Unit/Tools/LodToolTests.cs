using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Tools.LOD;

namespace Mosaic.Bridge.Tests.Unit.Tools
{
    [TestFixture]
    [Category("Unit")]
    public class LodToolTests
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("LodTestGO");
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void LodCreate_AddsLodGroup()
        {
            var result = LodCreateTool.Create(new LodCreateParams
            {
                Name = "LodTestGO",
                Levels = new[]
                {
                    new LodLevelInput { ScreenHeight = 0.6f },
                    new LodLevelInput { ScreenHeight = 0.3f },
                    new LodLevelInput { ScreenHeight = 0.1f }
                }
            });

            Assert.IsTrue(result.Success);
            Assert.AreEqual(3, result.Data.LodLevelCount);
            Assert.IsNotNull(_go.GetComponent<LODGroup>());
        }

        [Test]
        public void LodInfo_ReturnsLevels()
        {
            // Create a LODGroup manually
            var lodGroup = _go.AddComponent<LODGroup>();
            lodGroup.SetLODs(new[]
            {
                new UnityEngine.LOD(0.5f, new Renderer[0]),
                new UnityEngine.LOD(0.2f, new Renderer[0])
            });

            var result = LodInfoTool.Info(new LodInfoParams
            {
                Name = "LodTestGO"
            });

            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.Data.HasLodGroup);
            Assert.AreEqual(2, result.Data.Levels.Length);
        }

        [Test]
        public void LodInfo_NoLodGroup_ReturnsEmpty()
        {
            var result = LodInfoTool.Info(new LodInfoParams
            {
                Name = "LodTestGO"
            });

            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.Data.HasLodGroup);
        }
    }
}

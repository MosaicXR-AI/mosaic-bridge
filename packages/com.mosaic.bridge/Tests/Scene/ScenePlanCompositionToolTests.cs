using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Tools.Terrains;
using Mosaic.Bridge.Tools.Scene;

namespace Mosaic.Bridge.Tests.Scene
{
    [TestFixture]
    [Category("Scene")]
    public class ScenePlanCompositionToolTests
    {
        private GameObject _terrainGo;
        private string _assetPath;

        [SetUp]
        public void SetUp()
        {
            var r = TerrainCreateTool.Execute(new TerrainCreateParams
            {
                Name = "TestTerrain_ScenePlan",
                Width = 1000f, Length = 1000f, Height = 300f,
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
        public void PlanComposition_MinimalInput_ReturnsPlan()
        {
            var result = ScenePlanCompositionTool.Execute(new ScenePlanCompositionParams
            {
                SceneName = "TestScene",
                GeographicRef = "Test desert",
                TerrainSizeX = 1000f, TerrainSizeZ = 1000f,
                MaxHeightMeters = 300f
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.IsFalse(string.IsNullOrEmpty(result.Data.PlanId));
            Assert.AreEqual("TestScene", result.Data.SceneName);
            Assert.IsNotNull(result.Data.ExecutionPhases);
            Assert.Greater(result.Data.ExecutionPhases.Length, 0);
        }

        [Test]
        public void PlanComposition_LightingResolvesForTimeOfDay()
        {
            var dawn = ScenePlanCompositionTool.Execute(new ScenePlanCompositionParams
            {
                TimeOfDay = "dawn"
            });
            var midday = ScenePlanCompositionTool.Execute(new ScenePlanCompositionParams
            {
                TimeOfDay = "midday"
            });
            Assert.IsTrue(dawn.Success);
            Assert.IsTrue(midday.Success);
            // Dawn angle should be lower than midday
            Assert.Less(dawn.Data.Lighting.DirectionalAngle, midday.Data.Lighting.DirectionalAngle);
        }

        [Test]
        public void PlanComposition_SamplesTerrainHeightForLandmarks()
        {
            var result = ScenePlanCompositionTool.Execute(new ScenePlanCompositionParams
            {
                SceneName = "TestScene",
                TerrainSizeX = 1000f, TerrainSizeZ = 1000f,
                MaxHeightMeters = 300f,
                SampleExistingTerrain = true,
                Regions = new[]
                {
                    new ScenePlanRegion
                    {
                        Id = "r1", Name = "Desert",
                        XMin = 0f, XMax = 500f, ZMin = 0f, ZMax = 500f,
                        HeightRangeMin = 0f, HeightRangeMax = 50f,
                        Landmarks = new[]
                        {
                            new ScenePlanLandmark { Name = "Rock Formation", Type = "rock", PreferredX = 250f, PreferredZ = 250f }
                        }
                    }
                }
            });
            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.ObjectPlacements.Length);
            Assert.AreEqual("Rock Formation", result.Data.ObjectPlacements[0].LandmarkName);
            Assert.IsTrue(result.Data.ObjectPlacements[0].HeightSampled,
                "Terrain was active — height should have been sampled");
        }

        [Test]
        public void PlanComposition_NoTerrain_FallsBackToRegionHeight()
        {
            // Destroy terrain so none is active
            if (_terrainGo != null) Object.DestroyImmediate(_terrainGo);
            _terrainGo = null;

            var result = ScenePlanCompositionTool.Execute(new ScenePlanCompositionParams
            {
                SampleExistingTerrain = true,
                Regions = new[]
                {
                    new ScenePlanRegion
                    {
                        Id = "r1", Name = "Flat",
                        XMin = 0f, XMax = 100f, ZMin = 0f, ZMax = 100f,
                        HeightRangeMin = 15f,
                        Landmarks = new[]
                        {
                            new ScenePlanLandmark { Name = "Tree", Type = "tree" }
                        }
                    }
                }
            });
            Assert.IsTrue(result.Success, result.Error);
            // Should have warned about missing terrain
            Assert.Greater(result.Data.Warnings.Length, 0);
            // Y should fall back to HeightRangeMin + offset
            Assert.AreEqual(15f + 0.1f, result.Data.ObjectPlacements[0].WorldY, 0.001f);
        }

        [Test]
        public void PlanComposition_DronePlayer_HasCameraPhase()
        {
            var result = ScenePlanCompositionTool.Execute(new ScenePlanCompositionParams
            {
                PlayerType = "drone"
            });
            Assert.IsTrue(result.Success, result.Error);
            bool hasCameraPhase = System.Array.Exists(result.Data.ExecutionPhases,
                ph => ph.Name.Contains("Camera"));
            Assert.IsTrue(hasCameraPhase, "Drone player should produce a Camera phase");
        }

        [Test]
        public void PlanComposition_NonePlayer_NoCameraPhase()
        {
            var result = ScenePlanCompositionTool.Execute(new ScenePlanCompositionParams
            {
                PlayerType = "none"
            });
            Assert.IsTrue(result.Success, result.Error);
            bool hasCameraPhase = System.Array.Exists(result.Data.ExecutionPhases,
                ph => ph.Name.Contains("Camera"));
            Assert.IsFalse(hasCameraPhase, "PlayerType=none should not produce a Camera phase");
        }
    }
}

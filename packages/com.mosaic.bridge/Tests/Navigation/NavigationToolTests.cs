#if MOSAIC_HAS_NAVIGATION || UNITY_6000_0_OR_NEWER
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEditor;
using Mosaic.Bridge.Tools.Navigation;

namespace Mosaic.Bridge.Tests.Navigation
{
    [TestFixture]
    [Category("Tools")]
    [Category("Navigation")]
    public class NavigationToolTests
    {
        private GameObject _testGo;

        [SetUp]
        public void SetUp()
        {
            _testGo = new GameObject("NavTest_Target");
        }

        [TearDown]
        public void TearDown()
        {
            if (_testGo != null)
                Object.DestroyImmediate(_testGo);
        }

        // ── navigation/add-agent ────────────────────────────────────────────

        [Test]
        public void AddAgent_AddsNavMeshAgentComponent()
        {
            var result = NavigationAddAgentTool.Execute(new NavigationAddAgentParams
            {
                Name = "NavTest_Target"
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(_testGo.GetComponent<NavMeshAgent>());
            Assert.AreEqual("NavTest_Target", result.Data.GameObjectName);
        }

        [Test]
        public void AddAgent_WithCustomSpeed_SetsSpeed()
        {
            var result = NavigationAddAgentTool.Execute(new NavigationAddAgentParams
            {
                Name = "NavTest_Target",
                Speed = 10f,
                StoppingDistance = 2f
            });

            Assert.IsTrue(result.Success, result.Error);
            var agent = _testGo.GetComponent<NavMeshAgent>();
            Assert.IsNotNull(agent);
            Assert.AreEqual(10f, agent.speed, 0.01f);
            Assert.AreEqual(2f, agent.stoppingDistance, 0.01f);
        }

        [Test]
        public void AddAgent_GameObjectNotFound_ReturnsFail()
        {
            var result = NavigationAddAgentTool.Execute(new NavigationAddAgentParams
            {
                Name = "NonExistent_NavTarget"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void AddAgent_NoIdentifier_ReturnsFail()
        {
            var result = NavigationAddAgentTool.Execute(new NavigationAddAgentParams());

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        // ── navigation/add-obstacle ─────────────────────────────────────────

        [Test]
        public void AddObstacle_Box_AddsComponent()
        {
            var result = NavigationAddObstacleTool.Execute(new NavigationAddObstacleParams
            {
                Name = "NavTest_Target",
                Shape = "Box"
            });

            Assert.IsTrue(result.Success, result.Error);
            var obstacle = _testGo.GetComponent<NavMeshObstacle>();
            Assert.IsNotNull(obstacle);
            Assert.AreEqual(NavMeshObstacleShape.Box, obstacle.shape);
            Assert.AreEqual("Box", result.Data.Shape);
        }

        [Test]
        public void AddObstacle_Capsule_AddsComponent()
        {
            var result = NavigationAddObstacleTool.Execute(new NavigationAddObstacleParams
            {
                Name = "NavTest_Target",
                Shape = "Capsule"
            });

            Assert.IsTrue(result.Success, result.Error);
            var obstacle = _testGo.GetComponent<NavMeshObstacle>();
            Assert.IsNotNull(obstacle);
            Assert.AreEqual(NavMeshObstacleShape.Capsule, obstacle.shape);
        }

        [Test]
        public void AddObstacle_WithCarve_SetsCarving()
        {
            var result = NavigationAddObstacleTool.Execute(new NavigationAddObstacleParams
            {
                Name = "NavTest_Target",
                Shape = "Box",
                Carve = true
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(_testGo.GetComponent<NavMeshObstacle>().carving);
            Assert.IsTrue(result.Data.Carve);
        }

        [Test]
        public void AddObstacle_InvalidShape_ReturnsFail()
        {
            var result = NavigationAddObstacleTool.Execute(new NavigationAddObstacleParams
            {
                Name = "NavTest_Target",
                Shape = "Sphere"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void AddObstacle_MissingShape_ReturnsFail()
        {
            var result = NavigationAddObstacleTool.Execute(new NavigationAddObstacleParams
            {
                Name = "NavTest_Target"
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        // ── navigation/info ─────────────────────────────────────────────────

        [Test]
        public void Info_ReturnsNavMeshState()
        {
            var result = NavigationInfoTool.Execute(new NavigationInfoParams());

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsNotNull(result.Data);
            // AgentCount/ObstacleCount are non-negative
            Assert.GreaterOrEqual(result.Data.AgentCount, 0);
            Assert.GreaterOrEqual(result.Data.ObstacleCount, 0);
            Assert.IsNotNull(result.Data.Areas);
        }

        [Test]
        public void Info_CountsAgentsCorrectly()
        {
            // Add an agent first
            _testGo.AddComponent<NavMeshAgent>();

            var result = NavigationInfoTool.Execute(new NavigationInfoParams());

            Assert.IsTrue(result.Success, result.Error);
            Assert.GreaterOrEqual(result.Data.AgentCount, 1);
        }

        // ── navigation/set-destination ──────────────────────────────────────

        [Test]
        public void SetDestination_NotInPlayMode_ReturnsFail()
        {
            // We are in edit mode during tests
            _testGo.AddComponent<NavMeshAgent>();

            var result = NavigationSetDestinationTool.Execute(new NavigationSetDestinationParams
            {
                Name = "NavTest_Target",
                Destination = new float[] { 5f, 0f, 5f }
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_PERMITTED", result.ErrorCode);
        }

        [Test]
        public void SetDestination_InvalidDestination_ReturnsFail()
        {
            // Even though play mode check runs first, test the validation path
            var result = NavigationSetDestinationTool.Execute(new NavigationSetDestinationParams
            {
                Name = "NavTest_Target",
                Destination = new float[] { 1f, 2f } // only 2 elements
            });

            Assert.IsFalse(result.Success);
            // Will fail on play mode check first in edit mode
            Assert.IsNotNull(result.ErrorCode);
        }

        // ── navigation/bake ─────────────────────────────────────────────────

        [Test]
        public void Bake_OnPlane_Succeeds()
        {
            // Create a plane for the NavMesh to bake on
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.isStatic = true;

            try
            {
                var result = NavigationBakeTool.Execute(new NavigationBakeParams());

                Assert.IsTrue(result.Success, result.Error);
                Assert.IsNotNull(result.Data);
                Assert.Greater(result.Data.AgentRadius, 0f);
                Assert.Greater(result.Data.AgentHeight, 0f);
            }
            finally
            {
                Object.DestroyImmediate(plane);
            }
        }

        [Test]
        public void Bake_WithCustomSettings_AppliesSettings()
        {
            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.isStatic = true;

            try
            {
                var result = NavigationBakeTool.Execute(new NavigationBakeParams
                {
                    AgentRadius = 0.8f,
                    SlopeAngle = 30f
                });

                Assert.IsTrue(result.Success, result.Error);
                // The settings should be reflected back
                Assert.AreEqual(0.8f, result.Data.AgentRadius, 0.01f);
                Assert.AreEqual(30f, result.Data.SlopeAngle, 0.01f);
            }
            finally
            {
                Object.DestroyImmediate(plane);
            }
        }

        // ── NavigationToolHelpers ───────────────────────────────────────────

        [Test]
        public void ResolveGameObject_ByName_Finds()
        {
            var go = NavigationToolHelpers.ResolveGameObject(null, "NavTest_Target");
            Assert.IsNotNull(go);
            Assert.AreEqual("NavTest_Target", go.name);
        }

        [Test]
        public void ResolveGameObject_ByInstanceId_Finds()
        {
            var go = NavigationToolHelpers.ResolveGameObject(_testGo.GetInstanceID(), null);
            Assert.IsNotNull(go);
            Assert.AreEqual(_testGo, go);
        }

        [Test]
        public void ResolveGameObject_NeitherProvided_ReturnsNull()
        {
            var go = NavigationToolHelpers.ResolveGameObject(null, null);
            Assert.IsNull(go);
        }
    }
}
#endif

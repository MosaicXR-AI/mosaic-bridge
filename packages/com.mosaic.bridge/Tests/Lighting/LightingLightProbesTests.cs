using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Tools.Lighting;

namespace Mosaic.Bridge.Tests.Lighting
{
    // O4 §4.3: nothing in Mosaic read or wrote light probes before this — dynamic objects need
    // probes to be lit correctly in baked scenes.
    [TestFixture]
    [Category("Lighting")]
    public class LightingLightProbesTests
    {
        private const string GroupName = "__MosaicTest_LightProbeGroup__";
        private GameObject _ground;

        [TearDown]
        public void TearDown()
        {
            var groupGo = GameObject.Find(GroupName);
            if (groupGo != null) Object.DestroyImmediate(groupGo);
            if (_ground != null) Object.DestroyImmediate(_ground);
        }

        [Test]
        public void Create_MakesLightProbeGroup()
        {
            var result = LightingLightProbesTool.Execute(new LightingLightProbesParams
            {
                Action = "create", Name = GroupName, Position = new[] { 1f, 2f, 3f },
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0, result.Data.ProbeCount);
            var go = GameObject.Find(GroupName);
            Assert.IsNotNull(go.GetComponent<LightProbeGroup>());
            Assert.AreEqual(new Vector3(1, 2, 3), go.transform.position);
        }

        [Test]
        public void Grid_FillsBoundsWithoutRaycast()
        {
            LightingLightProbesTool.Execute(new LightingLightProbesParams { Action = "create", Name = GroupName });

            var result = LightingLightProbesTool.Execute(new LightingLightProbesParams
            {
                Action = "grid", Name = GroupName,
                BoundsMin = new[] { 0f, 0f, 0f }, BoundsMax = new[] { 2f, 2f, 2f },
                Spacing = new[] { 1f, 1f, 1f },
            });

            Assert.IsTrue(result.Success, result.Error);
            // 3 steps per axis (0,1,2) = 27 probes
            Assert.AreEqual(27, result.Data.ProbeCount);
        }

        [Test]
        public void Grid_RaycastAboveGround_PlacesProbesAtGroundHeightPlusOffset()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            _ground.transform.position = Vector3.zero; // Plane top surface is at y=0

            LightingLightProbesTool.Execute(new LightingLightProbesParams { Action = "create", Name = GroupName });

            var result = LightingLightProbesTool.Execute(new LightingLightProbesParams
            {
                Action = "grid", Name = GroupName,
                BoundsMin = new[] { -1f, 0f, -1f }, BoundsMax = new[] { 1f, 5f, 1f },
                Spacing = new[] { 1f, 1f, 1f }, RaycastAboveGround = true, HeightOffset = 0.5f,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.Greater(result.Data.ProbeCount, 0);
            var group = GameObject.Find(GroupName).GetComponent<LightProbeGroup>();
            foreach (var pos in group.probePositions)
                Assert.AreEqual(0.5f, pos.y, 0.01f, "probe should sit HeightOffset above the plane's ground surface");
        }

        [Test]
        public void AddPositions_AppendsToExisting()
        {
            LightingLightProbesTool.Execute(new LightingLightProbesParams { Action = "create", Name = GroupName });
            LightingLightProbesTool.Execute(new LightingLightProbesParams
            {
                Action = "add-positions", Name = GroupName, Positions = new[] { 0f, 0f, 0f },
            });

            var result = LightingLightProbesTool.Execute(new LightingLightProbesParams
            {
                Action = "add-positions", Name = GroupName, Positions = new[] { 1f, 1f, 1f, 2f, 2f, 2f },
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(3, result.Data.ProbeCount);
        }

        [Test]
        public void Clear_EmptiesProbePositions()
        {
            LightingLightProbesTool.Execute(new LightingLightProbesParams { Action = "create", Name = GroupName });
            LightingLightProbesTool.Execute(new LightingLightProbesParams
            {
                Action = "add-positions", Name = GroupName, Positions = new[] { 0f, 0f, 0f },
            });

            var result = LightingLightProbesTool.Execute(new LightingLightProbesParams
            {
                Action = "clear", Name = GroupName,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0, result.Data.ProbeCount);
        }

        [Test]
        public void Grid_GroupNotFound_ReturnsNotFound()
        {
            var result = LightingLightProbesTool.Execute(new LightingLightProbesParams
            {
                Action = "grid", Name = "NoSuchGroup",
                BoundsMin = new[] { 0f, 0f, 0f }, BoundsMax = new[] { 1f, 1f, 1f }, Spacing = new[] { 1f, 1f, 1f },
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("NOT_FOUND", result.ErrorCode);
        }

        [Test]
        public void Execute_UnknownAction_ReturnsInvalidParam()
        {
            var result = LightingLightProbesTool.Execute(new LightingLightProbesParams
            {
                Action = "bogus", Name = GroupName,
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}

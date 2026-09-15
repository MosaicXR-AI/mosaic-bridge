using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Tools.Physics;

namespace Mosaic.Bridge.Tests.Physics
{
    // O4 §4.7: physics/simulate — "drop crates and verify they land in the pit" without entering
    // Play Mode, and QA that a joint chain does not explode.
    [TestFixture]
    [Category("Physics")]
    public class PhysicsSimulateToolTests
    {
        private GameObject _falling;
        private GameObject _pinned;

        [SetUp]
        public void SetUp()
        {
            _falling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _falling.name = "SimulateTestFalling";
            _falling.transform.position = new Vector3(0f, 10f, 0f);
            var rb = _falling.AddComponent<Rigidbody>();
            rb.useGravity = true;
            rb.linearDamping = 0f;

            _pinned = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _pinned.name = "SimulateTestPinned";
            _pinned.transform.position = new Vector3(5f, 10f, 0f);
            var pinnedRb = _pinned.AddComponent<Rigidbody>();
            pinnedRb.useGravity = true;
        }

        [TearDown]
        public void TearDown()
        {
            if (_falling != null) Object.DestroyImmediate(_falling);
            if (_pinned != null) Object.DestroyImmediate(_pinned);
        }

        [Test]
        public void Simulate_MultipleSteps_MovesFallingBodyDown()
        {
            var result = PhysicsSimulateTool.Execute(new PhysicsSimulateParams
            {
                Steps = 20, StepSize = 0.02f,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(20, result.Data.StepsSimulated);

            var pose = System.Array.Find(result.Data.Poses, p => p.GameObjectName == "SimulateTestFalling");
            Assert.IsNotNull(pose);
            Assert.Less(pose.PositionAfter[1], pose.PositionBefore[1]);
        }

        [Test]
        public void Simulate_WithTargets_OnlyMovesTargetBody()
        {
            var result = PhysicsSimulateTool.Execute(new PhysicsSimulateParams
            {
                Steps = 20, StepSize = 0.02f, Targets = new[] { "SimulateTestFalling" },
            });

            Assert.IsTrue(result.Success, result.Error);

            var fallingPose = System.Array.Find(result.Data.Poses, p => p.GameObjectName == "SimulateTestFalling");
            var pinnedPose = System.Array.Find(result.Data.Poses, p => p.GameObjectName == "SimulateTestPinned");
            Assert.Less(fallingPose.PositionAfter[1], fallingPose.PositionBefore[1]);
            Assert.AreEqual(pinnedPose.PositionBefore[1], pinnedPose.PositionAfter[1], 0.001f,
                "the non-target body must have been temporarily forced kinematic and not moved");

            // Restored afterward — not left permanently kinematic.
            Assert.IsFalse(_pinned.GetComponent<Rigidbody>().isKinematic);
        }

        [Test]
        public void Simulate_ZeroSteps_ReturnsInvalidParam()
        {
            var result = PhysicsSimulateTool.Execute(new PhysicsSimulateParams { Steps = 0 });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }
    }
}

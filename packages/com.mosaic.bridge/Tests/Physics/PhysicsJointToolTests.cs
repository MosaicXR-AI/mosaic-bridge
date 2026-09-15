using NUnit.Framework;
using UnityEngine;
using Mosaic.Bridge.Tools.Physics;

namespace Mosaic.Bridge.Tests.Physics
{
    // O4 §4.7: physics/add-joint + physics/joint-info — hinged doors, spring-suspended crates,
    // pendulums, ragdoll-adjacent character joints, configurable constraints.
    [TestFixture]
    [Category("Physics")]
    public class PhysicsJointToolTests
    {
        private GameObject _host;
        private GameObject _target;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("JointTestHost");
            _target = new GameObject("JointTestTarget");
            _target.AddComponent<Rigidbody>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            if (_target != null) Object.DestroyImmediate(_target);
        }

        [Test]
        public void AddJoint_Hinge_WithLimitsMotorSpring_Applies()
        {
            var result = PhysicsAddJointTool.Execute(new PhysicsAddJointParams
            {
                Name = "JointTestHost", JointType = "Hinge", ConnectedBodyName = "JointTestTarget",
                UseLimits = true, LimitMin = -30f, LimitMax = 45f,
                UseMotor = true, MotorForce = 10f, MotorTargetVelocity = 90f,
                UseSpring = true, SpringForce = 5f, SpringDamper = 1f,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.HostRigidbodyAdded);

            var hinge = _host.GetComponent<HingeJoint>();
            Assert.IsNotNull(hinge);
            Assert.AreEqual(_target.GetComponent<Rigidbody>(), hinge.connectedBody);
            Assert.IsTrue(hinge.useLimits);
            Assert.AreEqual(-30f, hinge.limits.min, 0.001f);
            Assert.AreEqual(45f, hinge.limits.max, 0.001f);
            Assert.IsTrue(hinge.useMotor);
            Assert.AreEqual(90f, hinge.motor.targetVelocity, 0.001f);
            Assert.IsTrue(hinge.useSpring);
        }

        [Test]
        public void AddJoint_Fixed_ConnectsToWorld()
        {
            var result = PhysicsAddJointTool.Execute(new PhysicsAddJointParams
            {
                Name = "JointTestHost", JointType = "Fixed",
            });

            Assert.IsTrue(result.Success, result.Error);
            var fixedJoint = _host.GetComponent<FixedJoint>();
            Assert.IsNotNull(fixedJoint);
            Assert.IsNull(fixedJoint.connectedBody);
        }

        [Test]
        public void AddJoint_Spring_AppliesDistancesAndSpring()
        {
            var result = PhysicsAddJointTool.Execute(new PhysicsAddJointParams
            {
                Name = "JointTestHost", JointType = "Spring", ConnectedBodyName = "JointTestTarget",
                MinDistance = 1f, MaxDistance = 3f, SpringForce = 20f, SpringDamper = 2f,
            });

            Assert.IsTrue(result.Success, result.Error);
            var spring = _host.GetComponent<SpringJoint>();
            Assert.AreEqual(1f, spring.minDistance, 0.001f);
            Assert.AreEqual(3f, spring.maxDistance, 0.001f);
            Assert.AreEqual(20f, spring.spring, 0.001f);
        }

        [Test]
        public void AddJoint_Character_AppliesTwistAndSwingLimits()
        {
            var result = PhysicsAddJointTool.Execute(new PhysicsAddJointParams
            {
                Name = "JointTestHost", JointType = "Character",
                TwistLimitMin = -20f, TwistLimitMax = 20f, Swing1Limit = 30f, Swing2Limit = 15f,
            });

            Assert.IsTrue(result.Success, result.Error);
            var character = _host.GetComponent<CharacterJoint>();
            Assert.AreEqual(-20f, character.lowTwistLimit.limit, 0.001f);
            Assert.AreEqual(20f, character.highTwistLimit.limit, 0.001f);
            Assert.AreEqual(30f, character.swing1Limit.limit, 0.001f);
        }

        [Test]
        public void AddJoint_Configurable_MotionAndDrive_Apply()
        {
            var result = PhysicsAddJointTool.Execute(new PhysicsAddJointParams
            {
                Name = "JointTestHost", JointType = "Configurable",
                XMotion = "Locked", YMotion = "Limited", ZMotion = "Free",
                LinearLimit = 2f, XDrive = new[] { 100f, 10f, 500f },
            });

            Assert.IsTrue(result.Success, result.Error);
            var configurable = _host.GetComponent<ConfigurableJoint>();
            Assert.AreEqual(ConfigurableJointMotion.Locked, configurable.xMotion);
            Assert.AreEqual(ConfigurableJointMotion.Limited, configurable.yMotion);
            Assert.AreEqual(ConfigurableJointMotion.Free, configurable.zMotion);
            Assert.AreEqual(2f, configurable.linearLimit.limit, 0.001f);
            Assert.AreEqual(100f, configurable.xDrive.positionSpring, 0.001f);
        }

        [Test]
        public void AddJoint_UnknownMotion_ReturnsInvalidParam()
        {
            var result = PhysicsAddJointTool.Execute(new PhysicsAddJointParams
            {
                Name = "JointTestHost", JointType = "Configurable", XMotion = "Bouncy",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void AddJoint_ConnectedBodyWithoutRigidbody_ReturnsInvalidParam()
        {
            var bareTarget = new GameObject("BareJointTarget");
            try
            {
                var result = PhysicsAddJointTool.Execute(new PhysicsAddJointParams
                {
                    Name = "JointTestHost", JointType = "Fixed", ConnectedBodyName = "BareJointTarget",
                });

                Assert.IsFalse(result.Success);
                Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
            }
            finally
            {
                Object.DestroyImmediate(bareTarget);
            }
        }

        [Test]
        public void AddJoint_UnknownJointType_ReturnsInvalidParam()
        {
            var result = PhysicsAddJointTool.Execute(new PhysicsAddJointParams
            {
                Name = "JointTestHost", JointType = "Ragdoll",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void JointInfo_ReportsAddedJoint()
        {
            PhysicsAddJointTool.Execute(new PhysicsAddJointParams
            {
                Name = "JointTestHost", JointType = "Hinge", ConnectedBodyName = "JointTestTarget",
                BreakForce = 500f,
            });

            var result = PhysicsJointInfoTool.Execute(new PhysicsJointInfoParams { Name = "JointTestHost" });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(1, result.Data.Joints.Length);
            Assert.AreEqual("HingeJoint", result.Data.Joints[0].JointType);
            Assert.AreEqual("JointTestTarget", result.Data.Joints[0].ConnectedBodyName);
            Assert.AreEqual(500f, result.Data.Joints[0].BreakForce, 0.001f);
        }

        [Test]
        public void JointInfo_NoJoints_ReturnsEmptyArray()
        {
            var result = PhysicsJointInfoTool.Execute(new PhysicsJointInfoParams { Name = "JointTestHost" });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(0, result.Data.Joints.Length);
        }
    }
}

using NUnit.Framework;
using Mosaic.Bridge.Tools.Physics;

namespace Mosaic.Bridge.Tests.Physics
{
    // O4 §4.7: physics/settings — SerializedObject(ProjectSettings/DynamicsManager.asset) had no
    // route beyond physics/set-gravity.
    [TestFixture]
    [Category("Physics")]
    public class PhysicsSettingsToolTests
    {
        private PhysicsSettingsResult _original;

        [SetUp]
        public void SetUp()
        {
            var result = PhysicsSettingsTool.Execute(new PhysicsSettingsParams());
            Assert.IsTrue(result.Success, result.Error);
            _original = result.Data;
        }

        [TearDown]
        public void TearDown()
        {
            PhysicsSettingsTool.Execute(new PhysicsSettingsParams
            {
                Gravity = _original.Gravity,
                BounceThreshold = _original.BounceThreshold,
                DefaultContactOffset = _original.DefaultContactOffset,
                DefaultSolverIterations = _original.DefaultSolverIterations,
                DefaultSolverVelocityIterations = _original.DefaultSolverVelocityIterations,
                SleepThreshold = _original.SleepThreshold,
                DefaultMaxDepenetrationVelocity = _original.DefaultMaxDepenetrationVelocity,
                DefaultMaxAngularSpeed = _original.DefaultMaxAngularSpeed,
                SimulationMode = _original.SimulationMode,
                QueriesHitTriggers = _original.QueriesHitTriggers,
                QueriesHitBackfaces = _original.QueriesHitBackfaces,
                AutoSyncTransforms = _original.AutoSyncTransforms,
            });
        }

        [Test]
        public void Query_NoChanges_ReportsCurrentValues()
        {
            var result = PhysicsSettingsTool.Execute(new PhysicsSettingsParams());

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("No changes (read-only query)", result.Data.Message);
        }

        [Test]
        public void SetGravityAndBounceThreshold_Applies()
        {
            var result = PhysicsSettingsTool.Execute(new PhysicsSettingsParams
            {
                Gravity = new[] { 0f, -20f, 0f }, BounceThreshold = 3f,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(-20f, result.Data.Gravity[1], 0.001f);
            Assert.AreEqual(3f, result.Data.BounceThreshold, 0.001f);
            Assert.AreEqual(-20f, UnityEngine.Physics.gravity.y, 0.001f);
        }

        [Test]
        public void SetSimulationMode_Applies()
        {
            var result = PhysicsSettingsTool.Execute(new PhysicsSettingsParams
            {
                SimulationMode = "Script",
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("Script", result.Data.SimulationMode);
        }

        [Test]
        public void SetSimulationMode_Unknown_ReturnsInvalidParam()
        {
            var result = PhysicsSettingsTool.Execute(new PhysicsSettingsParams
            {
                SimulationMode = "Quantum",
            });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("INVALID_PARAM", result.ErrorCode);
        }

        [Test]
        public void SetSolverIterationsAndQueries_Apply()
        {
            var result = PhysicsSettingsTool.Execute(new PhysicsSettingsParams
            {
                DefaultSolverIterations = 10, DefaultSolverVelocityIterations = 2,
                QueriesHitTriggers = false, AutoSyncTransforms = true,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual(10, result.Data.DefaultSolverIterations);
            Assert.AreEqual(2, result.Data.DefaultSolverVelocityIterations);
            Assert.IsFalse(result.Data.QueriesHitTriggers);
            Assert.IsTrue(result.Data.AutoSyncTransforms);
        }
    }
}

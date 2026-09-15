using NUnit.Framework;
using UnityEditor;
using Mosaic.Bridge.Tools.Physics;

namespace Mosaic.Bridge.Tests.Physics
{
    // O4 §4.7: physics/debug-visualization — lab screenshots of collider geometry; QA "collider
    // matches mesh".
    [TestFixture]
    [Category("Physics")]
    public class PhysicsDebugVisualizationToolTests
    {
        private PhysicsDebugVisualizationResult _original;

        [SetUp]
        public void SetUp()
        {
            var result = PhysicsDebugVisualizationTool.Execute(new PhysicsDebugVisualizationParams());
            Assert.IsTrue(result.Success, result.Error);
            _original = result.Data;
        }

        [TearDown]
        public void TearDown()
        {
            PhysicsDebugVisualizationTool.Execute(new PhysicsDebugVisualizationParams
            {
                ShowCollisionGeometry = _original.ShowCollisionGeometry,
                ShowContacts = _original.ShowContacts,
                ShowTriggers = _original.ShowTriggers,
                ShowRigidbodies = _original.ShowRigidbodies,
                ShowKinematicBodies = _original.ShowKinematicBodies,
                ShowSleepingBodies = _original.ShowSleepingBodies,
            });
        }

        [Test]
        public void Query_NoChanges_ReportsCurrentValues()
        {
            var result = PhysicsDebugVisualizationTool.Execute(new PhysicsDebugVisualizationParams());

            Assert.IsTrue(result.Success, result.Error);
            Assert.AreEqual("No changes (read-only query)", result.Data.Message);
        }

        [Test]
        public void SetShowCollisionGeometryAndTriggers_Applies()
        {
            var result = PhysicsDebugVisualizationTool.Execute(new PhysicsDebugVisualizationParams
            {
                ShowCollisionGeometry = true, ShowTriggers = false,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsTrue(result.Data.ShowCollisionGeometry);
            Assert.IsFalse(result.Data.ShowTriggers);
            Assert.IsTrue(PhysicsVisualizationSettings.showCollisionGeometry);
            Assert.IsFalse(PhysicsVisualizationSettings.GetShowTriggers());
        }

        [Test]
        public void SetShowRigidbodiesAndKinematic_Applies()
        {
            var result = PhysicsDebugVisualizationTool.Execute(new PhysicsDebugVisualizationParams
            {
                ShowRigidbodies = false, ShowKinematicBodies = true, ShowSleepingBodies = true,
            });

            Assert.IsTrue(result.Success, result.Error);
            Assert.IsFalse(result.Data.ShowRigidbodies);
            Assert.IsTrue(result.Data.ShowKinematicBodies);
            Assert.IsTrue(result.Data.ShowSleepingBodies);
        }
    }
}

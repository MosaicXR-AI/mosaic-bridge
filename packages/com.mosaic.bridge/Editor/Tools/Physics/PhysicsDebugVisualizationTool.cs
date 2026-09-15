using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.Physics
{
    public static class PhysicsDebugVisualizationTool
    {
        [MosaicTool("physics/debug-visualization",
                    "Gets/sets the Scene view's physics debug visualization (PhysicsVisualizationSettings): " +
                    "ShowCollisionGeometry, ShowContacts, ShowTriggers, ShowRigidbodies, ShowKinematicBodies, " +
                    "ShowSleepingBodies. Pair with camera/screenshot-scene for lab screenshots of collider " +
                    "geometry or QA that a collider matches its mesh.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<PhysicsDebugVisualizationResult> Execute(PhysicsDebugVisualizationParams p)
        {
            if (p.ShowCollisionGeometry.HasValue)
                PhysicsVisualizationSettings.showCollisionGeometry = p.ShowCollisionGeometry.Value;
            if (p.ShowContacts.HasValue)
                PhysicsVisualizationSettings.showContacts = p.ShowContacts.Value;
            if (p.ShowTriggers.HasValue)
                PhysicsVisualizationSettings.SetShowTriggers(p.ShowTriggers.Value);
            if (p.ShowRigidbodies.HasValue)
                PhysicsVisualizationSettings.SetShowRigidbodies(p.ShowRigidbodies.Value);
            if (p.ShowKinematicBodies.HasValue)
                PhysicsVisualizationSettings.SetShowKinematicBodies(p.ShowKinematicBodies.Value);
            if (p.ShowSleepingBodies.HasValue)
                PhysicsVisualizationSettings.SetShowSleepingBodies(p.ShowSleepingBodies.Value);

            bool changed = p.ShowCollisionGeometry.HasValue || p.ShowContacts.HasValue || p.ShowTriggers.HasValue ||
                           p.ShowRigidbodies.HasValue || p.ShowKinematicBodies.HasValue || p.ShowSleepingBodies.HasValue;

            return ToolResult<PhysicsDebugVisualizationResult>.Ok(new PhysicsDebugVisualizationResult
            {
                ShowCollisionGeometry = PhysicsVisualizationSettings.showCollisionGeometry,
                ShowContacts          = PhysicsVisualizationSettings.showContacts,
                ShowTriggers          = PhysicsVisualizationSettings.GetShowTriggers(),
                ShowRigidbodies       = PhysicsVisualizationSettings.GetShowRigidbodies(),
                ShowKinematicBodies   = PhysicsVisualizationSettings.GetShowKinematicBodies(),
                ShowSleepingBodies    = PhysicsVisualizationSettings.GetShowSleepingBodies(),
                Message               = changed ? "Physics debug visualization updated" : "No changes (read-only query)",
            });
        }
    }
}

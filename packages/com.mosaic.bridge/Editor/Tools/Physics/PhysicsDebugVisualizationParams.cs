namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsDebugVisualizationParams
    {
        /// <summary>Show collider wireframes in the Scene view. Null leaves unchanged.</summary>
        public bool? ShowCollisionGeometry { get; set; }
        /// <summary>Show contact points. Null leaves unchanged.</summary>
        public bool? ShowContacts { get; set; }
        /// <summary>Whether trigger colliders are included in the visualization filter. Null leaves unchanged.</summary>
        public bool? ShowTriggers { get; set; }
        /// <summary>Whether (non-kinematic, non-sleeping) Rigidbodies are included. Null leaves unchanged.</summary>
        public bool? ShowRigidbodies { get; set; }
        public bool? ShowKinematicBodies { get; set; }
        public bool? ShowSleepingBodies { get; set; }
    }
}

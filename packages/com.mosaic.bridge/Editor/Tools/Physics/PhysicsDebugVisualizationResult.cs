namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsDebugVisualizationResult
    {
        public bool ShowCollisionGeometry { get; set; }
        public bool ShowContacts          { get; set; }
        public bool ShowTriggers          { get; set; }
        public bool ShowRigidbodies       { get; set; }
        public bool ShowKinematicBodies   { get; set; }
        public bool ShowSleepingBodies    { get; set; }
        public string Message             { get; set; }
    }
}

namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsAddJointResult
    {
        public string GameObjectName    { get; set; }
        public int    InstanceId        { get; set; }
        public string JointType         { get; set; }
        public string ConnectedBodyName { get; set; }
        public bool   HostRigidbodyAdded { get; set; }
        public float[] Anchor           { get; set; }
        public float[] ConnectedAnchor  { get; set; }
        public string Message           { get; set; }
    }
}

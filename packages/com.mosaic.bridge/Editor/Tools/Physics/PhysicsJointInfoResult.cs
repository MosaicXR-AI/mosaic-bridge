namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsJointInfo
    {
        public string JointType         { get; set; }
        public string ConnectedBodyName { get; set; }
        public float[] Anchor           { get; set; }
        public float[] ConnectedAnchor  { get; set; }
        public float[] Axis             { get; set; }
        public float  BreakForce        { get; set; }
        public float  BreakTorque       { get; set; }
        public bool   EnableCollision   { get; set; }
        public float  CurrentForce      { get; set; }
        public float  CurrentTorque     { get; set; }
    }

    public sealed class PhysicsJointInfoResult
    {
        public string GameObjectName { get; set; }
        public int    InstanceId     { get; set; }
        public PhysicsJointInfo[] Joints { get; set; }
    }
}

namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsAddRigidbodyResult
    {
        public string GameObjectName { get; set; }
        public int InstanceId { get; set; }
        public float Mass { get; set; }
        public float Drag { get; set; }
        public float AngularDrag { get; set; }
        public bool UseGravity { get; set; }
        public bool IsKinematic { get; set; }
    }
}

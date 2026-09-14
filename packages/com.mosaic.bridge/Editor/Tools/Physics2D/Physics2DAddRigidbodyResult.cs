namespace Mosaic.Bridge.Tools.Physics2D
{
    public sealed class Physics2DAddRigidbodyResult
    {
        public string GameObjectName { get; set; }
        public int InstanceId { get; set; }
        public string BodyType { get; set; }
        public float Mass { get; set; }
        public float GravityScale { get; set; }
        public bool FreezeRotation { get; set; }
        public string Interpolation { get; set; }
        public string CollisionDetectionMode { get; set; }
    }
}

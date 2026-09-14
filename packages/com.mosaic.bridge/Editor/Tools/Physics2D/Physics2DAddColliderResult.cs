namespace Mosaic.Bridge.Tools.Physics2D
{
    public sealed class Physics2DAddColliderResult
    {
        public string GameObjectName { get; set; }
        public int InstanceId { get; set; }
        public string ColliderType { get; set; }
        public bool IsTrigger { get; set; }
        public bool RigidbodyAdded { get; set; }
    }
}

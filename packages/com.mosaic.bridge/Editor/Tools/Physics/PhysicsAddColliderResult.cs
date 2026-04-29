namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsAddColliderResult
    {
        public string GameObjectName  { get; set; }
        public int    InstanceId      { get; set; }
        public string ColliderType    { get; set; }
        public bool   IsTrigger       { get; set; }
        public float[] Center         { get; set; }
        public float[] Size           { get; set; }
        public bool   RigidbodyAdded  { get; set; }
    }
}

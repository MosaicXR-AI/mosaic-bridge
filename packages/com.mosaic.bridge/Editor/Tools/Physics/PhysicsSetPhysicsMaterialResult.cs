namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsSetPhysicsMaterialResult
    {
        public string GameObjectName { get; set; }
        public int InstanceId { get; set; }
        public float DynamicFriction { get; set; }
        public float StaticFriction { get; set; }
        public float Bounciness { get; set; }
        public string AssetPath { get; set; }
        public bool SavedAsAsset { get; set; }
    }
}

namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsOverlapResult
    {
        public int Count { get; set; }
        public OverlapHit[] Colliders { get; set; }
    }

    public sealed class OverlapHit
    {
        public string ColliderName { get; set; }
        public string GameObjectName { get; set; }
        public int GameObjectInstanceId { get; set; }
    }
}

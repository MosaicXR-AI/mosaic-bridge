namespace Mosaic.Bridge.Tools.Tilemap
{
    public sealed class TilemapAddColliderResult
    {
        public string GameObjectName { get; set; }
        public bool RigidbodyAdded { get; set; }
        public bool TilemapColliderAdded { get; set; }
        public bool CompositeColliderAdded { get; set; }
        public bool EffectorAdded { get; set; }
    }
}

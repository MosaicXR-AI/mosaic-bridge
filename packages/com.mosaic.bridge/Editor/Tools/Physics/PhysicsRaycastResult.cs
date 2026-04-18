namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsRaycastResult
    {
        public bool Hit { get; set; }
        public float[] Point { get; set; }
        public float[] Normal { get; set; }
        public float Distance { get; set; }
        public string ColliderName { get; set; }
        public string GameObjectName { get; set; }
        public int GameObjectInstanceId { get; set; }
    }
}

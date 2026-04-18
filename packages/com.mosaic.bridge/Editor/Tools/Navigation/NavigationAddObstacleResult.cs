namespace Mosaic.Bridge.Tools.Navigation
{
    public sealed class NavigationAddObstacleResult
    {
        public int InstanceId { get; set; }
        public string GameObjectName { get; set; }
        public string Shape { get; set; }
        public float[] Size { get; set; }
        public bool Carve { get; set; }
    }
}

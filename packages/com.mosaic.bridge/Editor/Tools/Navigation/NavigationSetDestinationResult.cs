namespace Mosaic.Bridge.Tools.Navigation
{
    public sealed class NavigationSetDestinationResult
    {
        public int InstanceId { get; set; }
        public string GameObjectName { get; set; }
        public float[] Destination { get; set; }
        public bool PathPending { get; set; }
    }
}

namespace Mosaic.Bridge.Tools.AdvancedNavigation
{
    public sealed class NavSpherePathfindResult
    {
        public float[] Path                  { get; set; }
        public int     PathLength            { get; set; }
        public float   ArcDistance           { get; set; }
        public int?    GameObjectInstanceId  { get; set; }
    }
}

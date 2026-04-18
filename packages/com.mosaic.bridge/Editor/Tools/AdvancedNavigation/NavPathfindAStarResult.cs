namespace Mosaic.Bridge.Tools.AdvancedNavigation
{
    public sealed class NavPathfindAStarResult
    {
        public int[] Path                  { get; set; }
        public int   PathLength            { get; set; }
        public int   NodesExplored         { get; set; }
        public int?  GameObjectInstanceId  { get; set; }
    }
}

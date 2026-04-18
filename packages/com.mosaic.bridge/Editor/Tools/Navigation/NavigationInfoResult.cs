namespace Mosaic.Bridge.Tools.Navigation
{
    public sealed class NavigationInfoResult
    {
        public bool HasBakedNavMesh { get; set; }
        public int AgentCount { get; set; }
        public int ObstacleCount { get; set; }
        public string[] Areas { get; set; }
    }
}

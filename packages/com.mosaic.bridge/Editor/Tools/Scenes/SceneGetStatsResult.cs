namespace Mosaic.Bridge.Tools.Scenes
{
    public sealed class SceneGetStatsResult
    {
        public int TotalGameObjects { get; set; }
        public int ActiveGameObjects { get; set; }
        public int TotalComponents { get; set; }
        public string[] ActiveCameras { get; set; }
    }
}

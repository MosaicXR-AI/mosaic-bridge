namespace Mosaic.Bridge.Tools.LOD
{
    public sealed class LodInfoResult
    {
        public string GameObjectName { get; set; }
        public bool HasLodGroup { get; set; }
        public LodLevelInfo[] Levels { get; set; }
    }

    public sealed class LodLevelInfo
    {
        public int Index { get; set; }
        public float ScreenRelativeHeight { get; set; }
        public int RendererCount { get; set; }
        public string[] RendererNames { get; set; }
    }
}

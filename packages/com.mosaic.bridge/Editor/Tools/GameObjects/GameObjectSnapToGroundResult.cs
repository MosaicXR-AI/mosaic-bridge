namespace Mosaic.Bridge.Tools.GameObjects
{
    public sealed class GameObjectSnapToGroundResult
    {
        public string GameObjectName { get; set; }
        public string HierarchyPath { get; set; }
        public float PreviousY { get; set; }
        public float NewY { get; set; }
        public float TerrainHeight { get; set; }
        public float YOffset { get; set; }
        public string SnapMode { get; set; }
    }
}

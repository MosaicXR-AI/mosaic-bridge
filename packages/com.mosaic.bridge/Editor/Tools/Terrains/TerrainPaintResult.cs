namespace Mosaic.Bridge.Tools.Terrains
{
    public sealed class TerrainPaintResult
    {
        public string Action     { get; set; }
        public int    InstanceId { get; set; }
        public string Name       { get; set; }
        public int    LayerCount { get; set; }
        /// <summary>add-layer only: the index the layer ended up at on this terrain (may be an
        /// existing index when the layer was reused rather than newly added).</summary>
        public int?   LayerIndex { get; set; }
        public string Message    { get; set; }
    }
}

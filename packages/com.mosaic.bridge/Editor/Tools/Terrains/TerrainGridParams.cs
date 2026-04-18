namespace Mosaic.Bridge.Tools.Terrains
{
    public sealed class TerrainGridParams
    {
        public int   Rows                { get; set; } = 2;
        public int   Columns             { get; set; } = 2;
        public float TileWidth           { get; set; } = 500f;
        public float TileLength          { get; set; } = 500f;
        public float TileHeight          { get; set; } = 600f;
        public int   HeightmapResolution { get; set; } = 513;
        public string NamePrefix         { get; set; } = "Terrain";
    }
}

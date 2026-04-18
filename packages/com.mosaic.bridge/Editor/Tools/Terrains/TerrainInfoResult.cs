namespace Mosaic.Bridge.Tools.Terrains
{
    public sealed class TerrainInfoResult
    {
        public int    InstanceId          { get; set; }
        public string Name                { get; set; }
        public float  Width               { get; set; }
        public float  Length              { get; set; }
        public float  Height              { get; set; }
        public int    HeightmapResolution { get; set; }
        public int    AlphamapResolution  { get; set; }
        public int    DetailResolution    { get; set; }
        public int    LayerCount          { get; set; }
        public int    TreePrototypeCount  { get; set; }
        public int    TreeInstanceCount   { get; set; }
        public int    DetailPrototypeCount { get; set; }
        public float[] Position           { get; set; }
        public string TerrainDataAssetPath { get; set; }
    }
}

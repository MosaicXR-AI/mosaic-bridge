namespace Mosaic.Bridge.Tools.Terrains
{
    public sealed class TerrainCreateResult
    {
        public int    InstanceId          { get; set; }
        public string Name                { get; set; }
        public float  Width               { get; set; }
        public float  Length              { get; set; }
        public float  Height              { get; set; }
        public int    HeightmapResolution { get; set; }
        public string TerrainDataAssetPath { get; set; }
    }
}

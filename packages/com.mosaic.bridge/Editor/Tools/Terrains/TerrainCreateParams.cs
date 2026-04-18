namespace Mosaic.Bridge.Tools.Terrains
{
    public sealed class TerrainCreateParams
    {
        public string Name               { get; set; } = "Terrain";
        public float  Width              { get; set; } = 500f;
        public float  Length             { get; set; } = 500f;
        public float  Height             { get; set; } = 600f;
        public int    HeightmapResolution { get; set; } = 513;
        public float[] Position          { get; set; }  // [x,y,z] — null defaults to [0,0,0]
    }
}

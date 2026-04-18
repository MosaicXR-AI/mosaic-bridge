namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenTerrainParams
    {
        public int?    Width            { get; set; }
        public int?    Depth            { get; set; }
        public int?    Octaves          { get; set; }
        public float?  Persistence      { get; set; }
        public float?  Lacunarity       { get; set; }
        public int?    Seed             { get; set; }
        public float?  Scale            { get; set; }
        public float?  HeightMultiplier { get; set; }
        public string  OutputDirectory  { get; set; }
    }
}

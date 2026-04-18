namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenPlanetParams
    {
        public float?  Radius        { get; set; }
        public int?    Resolution    { get; set; }
        public float?  NoiseScale    { get; set; }
        public float?  NoiseStrength { get; set; }
        public int?    Octaves       { get; set; }
        public int?    Seed          { get; set; }
        public string  OutputDirectory { get; set; }
    }
}

namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenNoise3DTextureParams
    {
        public int?    Resolution      { get; set; }
        public string  NoiseType       { get; set; }
        public int?    Octaves         { get; set; }
        public float?  Persistence     { get; set; }
        public int?    Seed            { get; set; }
        public string  OutputDirectory { get; set; }
    }
}

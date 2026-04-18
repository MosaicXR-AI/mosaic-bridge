namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenMarchingCubesParams
    {
        public int?    GridSize        { get; set; }
        public float?  IsoLevel        { get; set; }
        public float?  NoiseScale      { get; set; }
        public string  OutputDirectory { get; set; }
    }
}

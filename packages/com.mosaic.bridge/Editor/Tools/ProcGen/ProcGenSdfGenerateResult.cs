namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenSdfGenerateResult
    {
        public string AssetPath   { get; set; }
        public int    Resolution  { get; set; }
        public string Source      { get; set; }
        public float  MinDistance { get; set; }
        public float  MaxDistance { get; set; }
        public string Operation   { get; set; }
    }
}

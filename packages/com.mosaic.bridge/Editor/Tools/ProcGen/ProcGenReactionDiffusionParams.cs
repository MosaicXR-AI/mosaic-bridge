namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenReactionDiffusionParams
    {
        public int?    Width          { get; set; }
        public int?    Height         { get; set; }
        public float?  FeedRate       { get; set; }
        public float?  KillRate       { get; set; }
        public float?  DiffusionRateA { get; set; }
        public float?  DiffusionRateB { get; set; }
        public float?  DeltaTime      { get; set; }
        public string  OutputDirectory { get; set; }
    }
}

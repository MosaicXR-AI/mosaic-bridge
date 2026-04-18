using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenPoissonSampleParams
    {
        [Required] public float Radius            { get; set; }
        public float?           RegionWidth       { get; set; }
        public float?           RegionHeight      { get; set; }
        public int?             RejectionSamples  { get; set; }
        public int?             Seed              { get; set; }
        public string           OutputDirectory   { get; set; }
    }
}

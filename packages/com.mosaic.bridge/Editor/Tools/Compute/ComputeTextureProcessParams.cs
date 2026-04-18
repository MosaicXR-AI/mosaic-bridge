using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Compute
{
    public sealed class ComputeTextureProcessParams
    {
        [Required] public string Operation { get; set; }
        [Required] public string SourceTexturePath { get; set; }
        public string OutputPath { get; set; }
        public int Radius { get; set; } = 4;
        public float DecayRate { get; set; } = 0.95f;
    }
}

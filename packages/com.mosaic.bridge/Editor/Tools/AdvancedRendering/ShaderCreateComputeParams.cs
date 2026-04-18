using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.AdvancedRendering
{
    public sealed class ShaderCreateComputeParams
    {
        [Required] public string Name            { get; set; }
        public string   KernelName      { get; set; }
        public int?     ThreadGroupSize { get; set; }
        public string   BufferType      { get; set; }
        public int?     BufferSize      { get; set; }
        public bool     IncludeNoise    { get; set; }
        public string   OutputDirectory { get; set; }
    }
}

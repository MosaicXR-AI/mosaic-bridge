using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Compute
{
    public sealed class ComputeCreateParams
    {
        [Required] public string Template { get; set; }
        [Required] public string Name { get; set; }
        public string OutputDirectory { get; set; } = "Assets/Generated/ComputeShaders";
    }
}

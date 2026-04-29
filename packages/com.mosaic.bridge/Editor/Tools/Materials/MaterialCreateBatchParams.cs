using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Materials
{
    public sealed class MaterialCreateBatchParams
    {
        [Required] public MaterialCreateBatchEntry[] Materials { get; set; }
        public bool OverwriteExisting { get; set; } = false;
    }

    public sealed class MaterialCreateBatchEntry
    {
        [Required] public string Path       { get; set; }
        public string            ShaderName { get; set; }
    }
}

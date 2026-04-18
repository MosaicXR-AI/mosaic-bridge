using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Assets
{
    public sealed class AssetImportParams
    {
        [Required] public string Path { get; set; }
    }
}

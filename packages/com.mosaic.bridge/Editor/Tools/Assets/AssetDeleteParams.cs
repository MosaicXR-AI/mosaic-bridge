using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Assets
{
    public sealed class AssetDeleteParams
    {
        [Required] public string Path { get; set; }
    }
}

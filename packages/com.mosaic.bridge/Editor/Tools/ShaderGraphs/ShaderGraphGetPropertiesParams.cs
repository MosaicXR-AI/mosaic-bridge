using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.ShaderGraphs
{
    public sealed class ShaderGraphGetPropertiesParams
    {
        [Required] public string AssetPath { get; set; }
    }
}

using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Graphics
{
    public sealed class GraphicsSetShaderParams
    {
        [Required] public string MaterialPath { get; set; }
        [Required] public string ShaderName { get; set; }
    }
}

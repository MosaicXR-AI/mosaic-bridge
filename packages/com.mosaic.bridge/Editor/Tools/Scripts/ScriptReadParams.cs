using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Scripts
{
    public sealed class ScriptReadParams
    {
        [Required] public string Path { get; set; }
    }
}

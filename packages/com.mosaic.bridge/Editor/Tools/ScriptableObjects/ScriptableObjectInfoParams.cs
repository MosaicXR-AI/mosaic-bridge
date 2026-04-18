using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.ScriptableObjects
{
    public sealed class ScriptableObjectInfoParams
    {
        [Required] public string AssetPath { get; set; }
    }
}

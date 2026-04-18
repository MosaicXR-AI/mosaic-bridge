using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.ScriptableObjects
{
    public sealed class ScriptableObjectCreateParams
    {
        [Required] public string TypeName { get; set; }
        [Required] public string AssetPath { get; set; }
    }
}

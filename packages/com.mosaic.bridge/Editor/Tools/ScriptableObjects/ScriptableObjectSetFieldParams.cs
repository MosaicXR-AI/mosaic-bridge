using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.ScriptableObjects
{
    public sealed class ScriptableObjectSetFieldParams
    {
        [Required] public string AssetPath { get; set; }
        [Required] public string FieldName { get; set; }
        [Required] public object Value { get; set; }
    }
}

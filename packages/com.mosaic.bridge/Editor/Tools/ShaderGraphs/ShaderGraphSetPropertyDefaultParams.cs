using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.ShaderGraphs
{
    public sealed class ShaderGraphSetPropertyDefaultParams
    {
        [Required] public string AssetPath    { get; set; }
        [Required] public string PropertyName { get; set; }
        /// <summary>The new default value as a JSON string (e.g. "1.0", "[1,0,0,1]", "\"texture_guid\"").</summary>
        [Required] public string Value        { get; set; }
    }
}

using Mosaic.Bridge.Contracts.Attributes;
using Newtonsoft.Json.Linq;

namespace Mosaic.Bridge.Tools.Meta
{
    public sealed class MetaAdvancedToolParams
    {
        /// <summary>The tool route to invoke, e.g. "procgen/terrain".</summary>
        [Required] public string ToolName { get; set; }
        /// <summary>Arguments to pass to the tool as a JSON object.</summary>
        public JObject Arguments { get; set; }
    }
}

using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Tools.Shared;
using Newtonsoft.Json;

namespace Mosaic.Bridge.Tools.Scripts
{
    public sealed class ScriptUpdateParams
    {
        [Required] public string Path    { get; set; }
        /// <summary>
        /// File text. Also accepts a JSON object or array — a caller writing an .asmdef or
        /// .json naturally sends the structure, and being told "Unexpected character
        /// encountered while parsing value: {" reads as malformed JSON when the JSON was
        /// fine and only the type was wrong.
        /// </summary>
        [Required]
        [JsonConverter(typeof(RawTextConverter))]
        public string Content { get; set; }
    }
}

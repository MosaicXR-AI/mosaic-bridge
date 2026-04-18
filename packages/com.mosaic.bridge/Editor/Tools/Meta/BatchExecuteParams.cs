using System.Collections.Generic;
using Mosaic.Bridge.Contracts.Attributes;
using Newtonsoft.Json.Linq;

namespace Mosaic.Bridge.Tools.Meta
{
    public sealed class BatchExecuteParams
    {
        /// <summary>Array of tool calls to execute sequentially.</summary>
        [Required] public List<BatchCall> Calls { get; set; }
        /// <summary>If true, stop executing on first failure. Default false.</summary>
        public bool StopOnError { get; set; }
    }

    public sealed class BatchCall
    {
        [Required] public string ToolName { get; set; }
        public JObject Arguments { get; set; }
    }
}

using System.Collections.Generic;
using Mosaic.Bridge.Core.Discovery;
using Mosaic.Bridge.Core.Server;
using Newtonsoft.Json.Linq;

namespace Mosaic.Bridge.Core.Pipeline
{
    /// <summary>
    /// Per-request state bag carried through all pipeline stages.
    /// Stages read tool metadata and append warnings, KB references, and screenshots.
    /// </summary>
    public sealed class ExecutionContext
    {
        /// <summary>The original HTTP request.</summary>
        public HandlerRequest Request { get; set; }

        /// <summary>The resolved tool name (e.g., "mosaic_gameobject_create").</summary>
        public string ToolName { get; set; }

        /// <summary>The tool registry entry with metadata. Null for unknown tools.</summary>
        public ToolRegistryEntry ToolEntry { get; set; }

        /// <summary>Parsed tool parameters as JSON. Null if no body or parse failure.</summary>
        public JObject Parameters { get; set; }

        /// <summary>The execution mode for this request.</summary>
        public ExecutionMode Mode { get; set; }

        /// <summary>Warnings accumulated across pipeline stages. Included in the response.</summary>
        public List<string> Warnings { get; } = new List<string>();

        /// <summary>Knowledge base entry IDs consulted during this execution.</summary>
        public List<string> KBReferences { get; } = new List<string>();

        /// <summary>Screenshots captured by the visual verification stage.</summary>
        public List<ScreenshotData> Screenshots { get; } = new List<ScreenshotData>();

        /// <summary>Review context summary built by the review stage.</summary>
        public string ReviewSummary { get; set; }
    }

    /// <summary>
    /// A single screenshot captured from a specific camera angle.
    /// </summary>
    public sealed class ScreenshotData
    {
        public string AngleLabel { get; set; }
        public string Base64Png { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}

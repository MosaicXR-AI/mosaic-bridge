using System.Collections.Generic;
using Newtonsoft.Json;

namespace Mosaic.Bridge.Contracts.Envelopes
{
    /// <summary>
    /// Standardized response envelope for all Mosaic Bridge tool calls.
    /// Per FR15: every tool returns a ToolResult&lt;T&gt; with success/failure, data, error info,
    /// warnings, undo group tracking, and knowledge base references.
    /// </summary>
    /// <remarks>
    /// The envelope is versioned via SchemaVersion so future changes do not silently break MCP clients.
    /// Per FR71 (contract versioning), only additive minor-version changes are allowed; breaking changes
    /// require a major version bump and a coordinated MCP server release.
    /// </remarks>
    /// <typeparam name="T">The success data type.</typeparam>
    public class ToolResult<T>
    {
        /// <summary>Schema version for the envelope itself (not the tool data).</summary>
        [JsonProperty("schemaVersion")]
        public string SchemaVersion { get; set; } = "1.0";

        /// <summary>True if the tool executed successfully.</summary>
        [JsonProperty("success")]
        public bool Success { get; set; }

        /// <summary>The tool's return data (only set when Success is true).</summary>
        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public T Data { get; set; }

        /// <summary>Human-readable error message (only set when Success is false).</summary>
        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public string Error { get; set; }

        /// <summary>Canonical error code from ErrorCodes (only set when Success is false).</summary>
        [JsonProperty("errorCode", NullValueHandling = NullValueHandling.Ignore)]
        public string ErrorCode { get; set; }

        /// <summary>Optional suggested fix shown to the user (only set when Success is false).</summary>
        [JsonProperty("suggestedFix", NullValueHandling = NullValueHandling.Ignore)]
        public string SuggestedFix { get; set; }

        /// <summary>Non-fatal warnings emitted during execution.</summary>
        [JsonProperty("warnings", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Warnings { get; set; }

        /// <summary>Unity Undo group identifier for this tool call (per FR7).</summary>
        [JsonProperty("undoGroup", NullValueHandling = NullValueHandling.Ignore)]
        public string UndoGroup { get; set; }

        /// <summary>Knowledge base entry IDs that were referenced during this tool call (per FR29).</summary>
        [JsonProperty("knowledgeBaseReferences", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> KnowledgeBaseReferences { get; set; }

        /// <summary>Tool execution time in milliseconds (set by the dispatcher, not the tool method).</summary>
        [JsonProperty("executionTimeMs", NullValueHandling = NullValueHandling.Ignore)]
        public long? ExecutionTimeMs { get; set; }

        // === Factory methods ===

        /// <summary>Create a successful result with data.</summary>
        public static ToolResult<T> Ok(T data, string undoGroup = null, List<string> knowledgeBaseReferences = null)
        {
            return new ToolResult<T>
            {
                Success = true,
                Data = data,
                UndoGroup = undoGroup,
                KnowledgeBaseReferences = knowledgeBaseReferences
            };
        }

        /// <summary>Create a successful result with data and warnings.</summary>
        public static ToolResult<T> OkWithWarnings(T data, params string[] warnings)
        {
            return new ToolResult<T>
            {
                Success = true,
                Data = data,
                Warnings = new List<string>(warnings)
            };
        }

        /// <summary>Create a failed result with an error message and code.</summary>
        public static ToolResult<T> Fail(string error, string errorCode, string suggestedFix = null)
        {
            return new ToolResult<T>
            {
                Success = false,
                Error = error,
                ErrorCode = errorCode,
                SuggestedFix = suggestedFix
            };
        }

        /// <summary>Create a cancelled result (per FR10 cancellation tokens).</summary>
        public static ToolResult<T> Cancelled()
        {
            return new ToolResult<T>
            {
                Success = false,
                Error = "Tool execution was cancelled.",
                ErrorCode = Errors.ErrorCodes.CANCELLED
            };
        }
    }
}

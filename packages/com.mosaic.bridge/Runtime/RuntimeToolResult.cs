using System.Collections.Generic;

namespace Mosaic.Bridge.Runtime
{
    /// <summary>
    /// Standardized response envelope for runtime tool calls.
    /// Mirrors <c>Mosaic.Bridge.Contracts.Envelopes.ToolResult&lt;T&gt;</c> for runtime use.
    /// </summary>
    /// <typeparam name="T">The success data type.</typeparam>
    public class RuntimeToolResult<T>
    {
        public string SchemaVersion { get; set; } = "1.0";
        public bool Success { get; set; }
        public T Data { get; set; }
        public string Error { get; set; }
        public string ErrorCode { get; set; }
        public string SuggestedFix { get; set; }
        public List<string> Warnings { get; set; }
        public List<string> KnowledgeBaseReferences { get; set; }
        public long? ExecutionTimeMs { get; set; }

        public static RuntimeToolResult<T> Ok(T data) => new RuntimeToolResult<T>
        {
            Success = true,
            Data = data
        };

        public static RuntimeToolResult<T> Fail(string error, string errorCode, string suggestedFix = null) =>
            new RuntimeToolResult<T>
            {
                Success = false,
                Error = error,
                ErrorCode = errorCode,
                SuggestedFix = suggestedFix
            };

        public static RuntimeToolResult<T> Cancelled() => new RuntimeToolResult<T>
        {
            Success = false,
            Error = "Tool execution was cancelled.",
            ErrorCode = "CANCELLED"
        };
    }
}

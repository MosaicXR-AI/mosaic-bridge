using System.Collections.Generic;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.ConsoleTools
{
    public static class ConsoleGetErrorsTool
    {
        [MosaicTool("console/get-errors",
                    "Retrieves entries from the Unity Console. By default returns errors AND warnings (IncludeWarnings=true). " +
                    "Set IncludeInfo=true to also include log messages. Reads both pre-existing messages (compile errors, import warnings) " +
                    "and new messages captured since domain reload. Call this first when diagnosing Unity issues.",
                    isReadOnly: true)]
        public static ToolResult<ConsoleGetErrorsResult> GetErrors(ConsoleGetErrorsParams p)
        {
            ConsoleLogBuffer.EnsureInitialized();

            int maxResults = p.MaxResults > 0 ? p.MaxResults : 50;

            // Always include errors; optionally include warnings and info
            var entries = ConsoleLogBuffer.GetEntries(
                includeInfo: p.IncludeInfo,
                includeWarnings: p.IncludeWarnings,
                includeErrors: true,
                maxResults: maxResults
            );

            return ToolResult<ConsoleGetErrorsResult>.Ok(new ConsoleGetErrorsResult
            {
                Entries = entries,
                Count = entries.Count,
                ReflectionAvailable = true  // Always true now — using public API
            });
        }
    }
}

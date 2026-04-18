using System.Collections.Generic;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.ConsoleTools
{
    public static class ConsoleGetErrorsTool
    {
        [MosaicTool("console/get-errors",
                    "Retrieves error (and optionally warning/info) entries from the Unity Console",
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

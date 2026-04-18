using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.ConsoleTools
{
    public static class ConsoleLogTool
    {
        [MosaicTool("console/log",
                    "Writes a message to the Unity Console at the specified log level (info, warning, error)",
                    isReadOnly: false)]
        public static ToolResult<ConsoleLogResult> Log(ConsoleLogParams p)
        {
            if (string.IsNullOrEmpty(p.Message))
                return ToolResult<ConsoleLogResult>.Fail(
                    "Message is required",
                    ErrorCodes.INVALID_PARAM);

            string level = p.Level?.ToLowerInvariant() ?? "info";

            switch (level)
            {
                case "warning":
                    Debug.LogWarning($"[MosaicBridge] {p.Message}");
                    break;
                case "error":
                    Debug.LogError($"[MosaicBridge] {p.Message}");
                    break;
                default:
                    Debug.Log($"[MosaicBridge] {p.Message}");
                    level = "info";
                    break;
            }

            return ToolResult<ConsoleLogResult>.Ok(new ConsoleLogResult
            {
                Message = p.Message,
                Level = level
            });
        }
    }
}

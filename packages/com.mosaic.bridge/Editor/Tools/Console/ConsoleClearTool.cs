using System.Reflection;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.ConsoleTools
{
    public static class ConsoleClearTool
    {
        [MosaicTool("console/clear",
                    "Clears all messages from the Unity Console window",
                    isReadOnly: false)]
        public static ToolResult<ConsoleClearResult> Clear(ConsoleClearParams p)
        {
            try
            {
                var logEntriesType = System.Type.GetType("UnityEditor.LogEntries,UnityEditor");
                if (logEntriesType == null)
                {
                    return ToolResult<ConsoleClearResult>.Ok(new ConsoleClearResult { Cleared = false });
                }

                var clearMethod = logEntriesType.GetMethod("Clear",
                    BindingFlags.Static | BindingFlags.Public);

                if (clearMethod == null)
                {
                    return ToolResult<ConsoleClearResult>.Ok(new ConsoleClearResult { Cleared = false });
                }

                clearMethod.Invoke(null, null);

                // Also clear our ring buffer so get-errors returns clean results
                ConsoleLogBuffer.Clear();

                return ToolResult<ConsoleClearResult>.Ok(new ConsoleClearResult { Cleared = true });
            }
            catch
            {
                return ToolResult<ConsoleClearResult>.Ok(new ConsoleClearResult { Cleared = false });
            }
        }
    }
}

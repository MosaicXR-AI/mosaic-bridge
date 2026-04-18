using System.IO;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Core.Diagnostics;

namespace Mosaic.Bridge.Tools.ConsoleTools
{
    public static class ConsoleLogPathTool
    {
        [MosaicTool("console/log-path",
                    "Returns the path to the timestamped Unity console log file and optionally the last N lines",
                    isReadOnly: true)]
        public static ToolResult<ConsoleLogPathResult> Execute(ConsoleLogPathParams p)
        {
            var path = ConsoleLogCapture.GetLogPath();
            var result = new ConsoleLogPathResult { Path = path, Exists = File.Exists(path) };

            if (result.Exists && p.TailLines > 0)
            {
                var lines = File.ReadAllLines(path);
                int start = System.Math.Max(0, lines.Length - p.TailLines);
                result.RecentLines = new string[lines.Length - start];
                System.Array.Copy(lines, start, result.RecentLines, 0, result.RecentLines.Length);
                result.TotalLines = lines.Length;
            }

            return ToolResult<ConsoleLogPathResult>.Ok(result);
        }
    }
}

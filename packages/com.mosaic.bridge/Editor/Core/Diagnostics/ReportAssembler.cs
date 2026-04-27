using System;
using System.Text;
using UnityEngine;

namespace Mosaic.Bridge.Core.Diagnostics
{
    /// <summary>
    /// Assembles a structured diagnostic report as a plain-text string.
    /// Used by both mosaic_report_issue MCP tool and the Mosaic > Report Issue menu item.
    /// Constructed per-call — holds no shared mutable state.
    /// Trace sourced from ToolCallLogger (in-memory ring buffer); errors from ConsoleLogCapture.
    /// </summary>
    public sealed class ReportAssembler
    {
        private const int MaxBytes = 65_536; // 64 KB
        private const string TruncationMarker = "\n[Report truncated at 64KB]";
        private const int MaxToolCallRecords = 50;
        private const int MaxConsoleErrors = 10;

        /// <summary>
        /// Builds the formatted diagnostic report. Never throws.
        /// </summary>
        /// <param name="userDescription">Optional user-provided issue description.</param>
        public string BuildReport(string userDescription = "")
        {
            var sb = new StringBuilder(8192);

            try
            {
                sb.AppendLine("## Mosaic Issue Report");
                sb.AppendLine($"Date: {DateTime.UtcNow:o}");
                sb.AppendLine($"Unity Version: {Application.unityVersion}");
#if UNITY_EDITOR
                sb.AppendLine($"Mosaic Version: {MosaicVersion.Current}");
#endif
                sb.AppendLine($"Project: {Application.productName}");
                sb.AppendLine();
            }
            catch
            {
                sb.AppendLine("## Mosaic Issue Report");
                sb.AppendLine($"Date: {DateTime.UtcNow:o}");
                sb.AppendLine();
            }

            AppendToolCallTrace(sb);
            sb.AppendLine();
            AppendConsoleErrors(sb);
            sb.AppendLine();
            AppendUserDescription(sb, userDescription);

            return Truncate(sb.ToString());
        }

        private static void AppendToolCallTrace(StringBuilder sb)
        {
            sb.AppendLine("## Tool Call Trace");
            try
            {
                var records = ToolCallLogger.GetRecords(MaxToolCallRecords);
                if (records.Count == 0)
                {
                    sb.AppendLine("(no tool calls recorded)");
                    return;
                }

                foreach (var r in records)
                {
                    var outcome = r.IsSuccess ? "success" : "error";
                    sb.AppendLine($"{r.ToolName} → {outcome} ({r.DurationMs:F0}ms)");
                }
            }
            catch
            {
                sb.AppendLine("[Log unavailable]");
            }
        }

        private static void AppendConsoleErrors(StringBuilder sb)
        {
            sb.AppendLine($"## Console Errors (last {MaxConsoleErrors})");
            try
            {
                var errors = ConsoleLogCapture.GetLastErrors(MaxConsoleErrors);
                if (errors.Count == 0)
                {
                    sb.AppendLine("(none)");
                    return;
                }

                foreach (var e in errors)
                    sb.AppendLine($"[{e.LogType}] {e.Message}");
            }
            catch
            {
                sb.AppendLine("(console capture unavailable)");
            }
        }

        private static void AppendUserDescription(StringBuilder sb, string userDescription)
        {
            sb.AppendLine("## User Description");
            sb.AppendLine(string.IsNullOrWhiteSpace(userDescription)
                ? "[Describe the issue here]"
                : userDescription);
        }

        private static string Truncate(string report)
        {
            if (Encoding.UTF8.GetByteCount(report) <= MaxBytes) return report;

            var markerBytes = Encoding.UTF8.GetByteCount(TruncationMarker);
            var limit = MaxBytes - markerBytes;

            var allBytes = Encoding.UTF8.GetBytes(report);
            var cutoff = Math.Min(limit, allBytes.Length);

            // Walk back to the start of a valid UTF-8 sequence (avoid splitting multibyte chars)
            while (cutoff > 0 && (allBytes[cutoff] & 0xC0) == 0x80)
                cutoff--;

            return Encoding.UTF8.GetString(allBytes, 0, cutoff) + TruncationMarker;
        }
    }
}

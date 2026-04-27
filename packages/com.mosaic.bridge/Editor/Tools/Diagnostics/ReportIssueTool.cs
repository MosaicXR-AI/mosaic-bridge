using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Core.Diagnostics;
using Mosaic.Bridge.Core.Platform;

namespace Mosaic.Bridge.Tools.Diagnostics
{
    public static class ReportIssueTool
    {
        [MosaicTool("report/issue",
                    "Assembles a diagnostic report (tool call trace, console errors, system info) and copies it to the clipboard. Paste into a GitHub issue or support thread.",
                    isReadOnly: false)]
        public static ToolResult<ReportIssueResult> ReportIssue(ReportIssueParams p)
        {
            // Constructed per-call — no shared mutable state
            var assembler = new ReportAssembler();
            var report = assembler.BuildReport(p.UserDescription ?? "");

            if (ClipboardService.TryWrite(report, out _))
            {
                return ToolResult<ReportIssueResult>.Ok(new ReportIssueResult
                {
                    Message = "Issue report copied to clipboard. Paste it into your GitHub issue or support thread.",
                    ClipboardWritten = true
                });
            }

            // Clipboard unavailable — return the report text so the caller can still use it
            return ToolResult<ReportIssueResult>.Ok(new ReportIssueResult
            {
                Message = "Clipboard write unavailable. The full report is in reportText.",
                ReportText = report,
                ClipboardWritten = false
            });
        }
    }
}

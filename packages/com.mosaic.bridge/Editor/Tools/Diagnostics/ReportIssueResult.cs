namespace Mosaic.Bridge.Tools.Diagnostics
{
    public sealed class ReportIssueResult
    {
        /// <summary>Human-readable status message.</summary>
        public string Message { get; set; }

        /// <summary>The full report text. Only present when clipboard write failed.</summary>
        public string ReportText { get; set; }

        /// <summary>True if the report was written to the clipboard successfully.</summary>
        public bool ClipboardWritten { get; set; }
    }
}

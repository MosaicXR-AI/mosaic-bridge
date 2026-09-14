namespace Mosaic.Bridge.Tools.Jobs
{
    public sealed class JobStatusResult
    {
        public string JobId { get; set; }
        public string Kind { get; set; }

        /// <summary>"pending" | "running" | "done" | "failed" | "cancelled" | "interrupted".</summary>
        public string Status { get; set; }
        public string Message { get; set; }

        /// <summary>Set only once Status is "done" — the job kind's own result payload, as JSON text.</summary>
        public string ResultJson { get; set; }
    }
}

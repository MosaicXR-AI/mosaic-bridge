namespace Mosaic.Bridge.Core.Jobs
{
    /// <summary>
    /// Durable state for one job, persisted to SessionState after every state change (see
    /// JobRegistry). ParamsJson is kept alongside the live in-memory request object precisely
    /// because that object is expected to die in a domain reload — ParamsJson is what a job
    /// kind's Probe uses to re-derive the real outcome from evidence on disk instead (e.g. "is
    /// this package actually installed now") when the object it started with is gone.
    /// </summary>
    public sealed class JobRecord
    {
        public string JobId { get; set; }
        public string Kind { get; set; }
        public string StartedUtc { get; set; }
        public string ParamsJson { get; set; }
        public JobStatus Status { get; set; }
        public string Message { get; set; }

        /// <summary>Set only once Status is Done — the job kind's own result payload, as JSON.</summary>
        public string ResultJson { get; set; }
    }
}

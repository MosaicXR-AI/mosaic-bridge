namespace Mosaic.Bridge.Core.Jobs
{
    /// <summary>
    /// Terminal states are Done, Failed, Cancelled and Interrupted — a caller polling job/status
    /// should stop once it sees any of those four. Interrupted is distinct from Failed: it means
    /// a domain reload happened before the job kind's own Probe could determine the real outcome,
    /// not that the underlying operation is known to have failed.
    /// </summary>
    public enum JobStatus
    {
        Pending,
        Running,
        Done,
        Failed,
        Cancelled,
        Interrupted,
    }
}

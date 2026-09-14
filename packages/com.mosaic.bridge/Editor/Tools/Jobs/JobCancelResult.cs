namespace Mosaic.Bridge.Tools.Jobs
{
    public sealed class JobCancelResult
    {
        public string JobId { get; set; }
        public bool Cancelled { get; set; }
    }
}

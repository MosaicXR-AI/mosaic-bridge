namespace Mosaic.Bridge.Tools.Jobs
{
    public sealed class JobListParams
    {
        /// <summary>Optional: only jobs of this kind, e.g. "package/add".</summary>
        public string Kind { get; set; }
    }
}

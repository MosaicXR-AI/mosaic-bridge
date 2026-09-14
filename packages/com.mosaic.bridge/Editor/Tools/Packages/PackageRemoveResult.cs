namespace Mosaic.Bridge.Tools.Packages
{
    public sealed class PackageRemoveResult
    {
        /// <summary>Poll with job/status. Never blocks — see package/add's own note (L15).</summary>
        public string JobId { get; set; }

        /// <summary>Always "pending" at this point; job/status reports the real outcome.</summary>
        public string Status { get; set; }
        public string Message { get; set; }
    }
}

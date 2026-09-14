namespace Mosaic.Bridge.Tools.Packages
{
    public sealed class PackageAddResult
    {
        /// <summary>Poll with job/status. Never blocks — see L15 in the O4 findings for why
        /// this route used to and why that was dangerous.</summary>
        public string JobId { get; set; }

        /// <summary>Always "pending" at this point; job/status reports the real outcome.</summary>
        public string Status { get; set; }
        public string Message { get; set; }
    }
}

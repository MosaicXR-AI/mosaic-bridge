namespace Mosaic.Bridge.Tools.Packages
{
    public sealed class PackageRemoveResult
    {
        public string Name    { get; set; }
        public bool   Removed { get; set; }
        public string Message { get; set; }
    }
}

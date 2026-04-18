#if MOSAIC_HAS_ADDRESSABLES
namespace Mosaic.Bridge.Tools.Addressables
{
    public sealed class AddressablesBuildResult
    {
        public bool BuildSucceeded { get; set; }
        public string Error { get; set; }
        public long DurationMs { get; set; }
        public int FileCount { get; set; }
        public bool CleanBuildPerformed { get; set; }
    }
}
#endif

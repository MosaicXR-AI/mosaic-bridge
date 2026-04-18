#if MOSAIC_HAS_HDRP
namespace Mosaic.Bridge.Tools.HDRP
{
    public sealed class HdrpVolumeResult
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string HierarchyPath { get; set; }
        public bool IsGlobal { get; set; }
        public float Priority { get; set; }
        public string ProfileName { get; set; }
        public string[] EnabledOverrides { get; set; }
    }
}
#endif

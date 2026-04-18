#if MOSAIC_HAS_TMP
namespace Mosaic.Bridge.Tools.TextMeshPro
{
    public sealed class TmpCreateResult
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string HierarchyPath { get; set; }
        public string ContextType { get; set; }
    }
}
#endif

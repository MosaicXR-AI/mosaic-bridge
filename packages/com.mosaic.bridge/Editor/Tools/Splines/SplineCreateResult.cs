#if MOSAIC_HAS_SPLINES
namespace Mosaic.Bridge.Tools.Splines
{
    public sealed class SplineCreateResult
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string HierarchyPath { get; set; }
        public int KnotCount { get; set; }
        public bool Closed { get; set; }
        public float Length { get; set; }
    }
}
#endif

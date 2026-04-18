#if MOSAIC_HAS_SPLINES
namespace Mosaic.Bridge.Tools.Splines
{
    public sealed class SplineInfoResult
    {
        public bool IsReadOnly { get; set; } = true;
        public SplineContainerInfo[] Splines { get; set; }
    }

    public sealed class SplineContainerInfo
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string HierarchyPath { get; set; }
        public int KnotCount { get; set; }
        public float Length { get; set; }
        public bool Closed { get; set; }
        public SplineKnotInfo[] Knots { get; set; }
    }

    public sealed class SplineKnotInfo
    {
        public int Index { get; set; }
        public float[] Position { get; set; }
        public float[] Rotation { get; set; }
        public float[] TangentIn { get; set; }
        public float[] TangentOut { get; set; }
    }
}
#endif

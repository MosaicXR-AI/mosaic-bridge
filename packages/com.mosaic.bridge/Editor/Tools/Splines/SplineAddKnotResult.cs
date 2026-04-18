#if MOSAIC_HAS_SPLINES
namespace Mosaic.Bridge.Tools.Splines
{
    public sealed class SplineAddKnotResult
    {
        public string GameObjectName { get; set; }
        public string Action { get; set; }
        public int Index { get; set; }
        public int KnotCount { get; set; }
        public float Length { get; set; }
        public bool Closed { get; set; }
    }
}
#endif

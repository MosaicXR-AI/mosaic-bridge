namespace Mosaic.Bridge.Tools.Spatial
{
    public sealed class SpatialKdtreeCreateResult
    {
        public string StructureId  { get; set; }
        public int    Dimensions   { get; set; }
        public int    PointCount   { get; set; }
        public int    TreeDepth    { get; set; }
        public long   BuildTimeMs  { get; set; }
    }
}

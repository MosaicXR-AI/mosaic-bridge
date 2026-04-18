namespace Mosaic.Bridge.Tools.Spatial
{
    public sealed class SpatialOctreeCreateResult
    {
        public string StructureId     { get; set; }
        public int    Dimensions      { get; set; }
        public int    PointCount      { get; set; }
        public int    NodeCount       { get; set; }
        public int    MaxDepthReached { get; set; }
    }
}

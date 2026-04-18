namespace Mosaic.Bridge.Tools.Spatial
{
    public sealed class SpatialHashCreateResult
    {
        public string StructureId     { get; set; }
        public int    Dimensions      { get; set; }
        public float  CellSize        { get; set; }
        public int    PointCount      { get; set; }
        public int    CellCount       { get; set; }
        public int    MaxPointsInCell { get; set; }
    }
}

namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenVoronoiResult
    {
        public int              CellCount   { get; set; }
        public VoronoiCellInfo[] Cells      { get; set; }
        public string           MeshPath    { get; set; }
        public string           TexturePath { get; set; }
    }

    public sealed class VoronoiCellInfo
    {
        public float[] Center        { get; set; }
        public int     VertexCount   { get; set; }
        public int     NeighborCount { get; set; }
    }
}

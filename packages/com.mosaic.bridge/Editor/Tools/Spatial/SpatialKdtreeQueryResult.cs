namespace Mosaic.Bridge.Tools.Spatial
{
    public sealed class SpatialKdtreeQueryResult
    {
        public string      StructureId   { get; set; }
        public string      QueryType     { get; set; }
        public ResultPoint[] Points      { get; set; }
        public int         Count         { get; set; }
        public int         NodesVisited  { get; set; }
        public long        QueryTimeMs   { get; set; }

        public sealed class ResultPoint
        {
            public string  Id       { get; set; }
            public float[] Position { get; set; }
            public string  Data     { get; set; }
            public float   Distance { get; set; }
        }
    }
}

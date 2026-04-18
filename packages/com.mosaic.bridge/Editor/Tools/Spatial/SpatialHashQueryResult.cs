using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Spatial
{
    public sealed class SpatialHashQueryHit
    {
        public string  Id       { get; set; }
        public float[] Position { get; set; }
        public string  Data     { get; set; }
        public float   Distance { get; set; }
    }

    public sealed class SpatialHashQueryResult
    {
        public string                    StructureId  { get; set; }
        public string                    QueryType    { get; set; }
        public List<SpatialHashQueryHit> Points       { get; set; }
        public int                       Count        { get; set; }
        public int                       CellsVisited { get; set; }
    }
}

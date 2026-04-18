using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Spatial
{
    public sealed class SpatialOctreeQueryResult
    {
        public sealed class QueryPoint
        {
            public string  Id       { get; set; }
            public float[] Position { get; set; }
            public string  Data     { get; set; }
            public float   Distance { get; set; }
        }

        public string           StructureId  { get; set; }
        public string           QueryType    { get; set; }
        public List<QueryPoint> Points       { get; set; }
        public int              Count        { get; set; }
        public int              NodesVisited { get; set; }
    }
}

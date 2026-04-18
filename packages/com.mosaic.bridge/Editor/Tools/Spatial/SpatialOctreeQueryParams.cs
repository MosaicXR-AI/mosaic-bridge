using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Spatial
{
    public sealed class SpatialOctreeQueryParams
    {
        [Required] public string  StructureId { get; set; }
        [Required] public string  QueryType   { get; set; }
        public float[]            Position    { get; set; }
        public float[][]          Range       { get; set; }
        public int                K           { get; set; } = 1;
        public float              Radius      { get; set; }
    }
}

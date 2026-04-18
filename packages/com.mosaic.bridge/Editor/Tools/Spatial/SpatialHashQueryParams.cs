using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Spatial
{
    public sealed class SpatialHashQueryParams
    {
        [Required] public string  StructureId { get; set; }
        [Required] public string  QueryType   { get; set; }
        public            float[] Position    { get; set; }
        public            float   Radius      { get; set; }
        public            int[]   CellCoord   { get; set; }
        public            float[] RangeMin    { get; set; }
        public            float[] RangeMax    { get; set; }
    }
}

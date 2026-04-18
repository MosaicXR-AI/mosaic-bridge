using System.Collections.Generic;
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Spatial
{
    public sealed class SpatialHashPoint
    {
        public string  Id       { get; set; }
        public float[] Position { get; set; }
        public string  Data     { get; set; }
    }

    public sealed class SpatialHashCreateParams
    {
        [Required] public string                 StructureId { get; set; }
        [Required] public float                  CellSize    { get; set; }
        public            int                    Dimensions  { get; set; } = 3;
        public            List<SpatialHashPoint> Points      { get; set; }
        public            string[]               GameObjects { get; set; }
    }
}

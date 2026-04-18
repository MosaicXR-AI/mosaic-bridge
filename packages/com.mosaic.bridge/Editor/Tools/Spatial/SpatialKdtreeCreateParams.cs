using System.Collections.Generic;
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Spatial
{
    public sealed class SpatialKdtreeCreateParams
    {
        [Required] public string StructureId { get; set; }

        public int Dimensions { get; set; } = 3;

        public List<Point> Points { get; set; }

        public string[] GameObjects { get; set; }

        public sealed class Point
        {
            public string  Id       { get; set; }
            public float[] Position { get; set; }
            public string  Data     { get; set; }
        }
    }
}

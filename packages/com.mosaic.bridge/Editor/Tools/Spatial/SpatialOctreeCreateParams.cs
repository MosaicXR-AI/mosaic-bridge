using System.Collections.Generic;
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Spatial
{
    public sealed class SpatialOctreeCreateParams
    {
        public sealed class Point
        {
            public string  Id       { get; set; }
            public float[] Position { get; set; }
            public string  Data     { get; set; }
        }

        [Required] public string     StructureId      { get; set; }
        public int                   Dimensions       { get; set; } = 3;
        [Required] public float[]    BoundsMin        { get; set; }
        [Required] public float[]    BoundsMax        { get; set; }
        public int                   MaxDepth         { get; set; } = 6;
        public int                   MaxPointsPerNode { get; set; } = 8;
        public List<Point>           Points           { get; set; }
        public string[]              GameObjects      { get; set; }
    }
}

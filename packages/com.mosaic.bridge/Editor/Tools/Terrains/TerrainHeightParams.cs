using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Terrains
{
    public sealed class TerrainHeightParams
    {
        public int    InstanceId { get; set; }
        public string Name       { get; set; }

        [Required] public string Action { get; set; } // set, flatten, raise-lower, smooth, noise

        /// <summary>Normalized X position on the terrain (0..1).</summary>
        public float X { get; set; } = 0.5f;

        /// <summary>Normalized Y position on the terrain (0..1).</summary>
        public float Y { get; set; } = 0.5f;

        /// <summary>Brush radius in heightmap samples.</summary>
        public int Radius { get; set; } = 10;

        /// <summary>Strength of the operation (0..1).</summary>
        public float Strength { get; set; } = 0.5f;

        /// <summary>Target height for set/flatten actions (normalized 0..1).</summary>
        public float Height { get; set; } = 0f;

        /// <summary>Seed for noise action.</summary>
        public int Seed { get; set; } = 0;
    }
}

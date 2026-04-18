using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Terrains
{
    public sealed class TerrainDetailParams
    {
        public int    InstanceId { get; set; }
        public string Name       { get; set; }

        [Required] public string Action { get; set; } // add-prototype, paint, scatter, clear

        /// <summary>Texture asset path for grass detail prototype.</summary>
        public string TexturePath { get; set; }

        /// <summary>Prefab path for mesh detail prototype.</summary>
        public string PrefabPath { get; set; }

        /// <summary>Detail prototype index for paint/scatter.</summary>
        public int PrototypeIndex { get; set; }

        /// <summary>Normalized X position (0..1) for paint.</summary>
        public float X { get; set; } = 0.5f;

        /// <summary>Normalized Y position (0..1) for paint.</summary>
        public float Y { get; set; } = 0.5f;

        /// <summary>Brush radius in detail resolution samples.</summary>
        public int Radius { get; set; } = 10;

        /// <summary>Detail density value (0..16 per cell).</summary>
        public int Density { get; set; } = 8;

        /// <summary>Random seed for scatter.</summary>
        public int Seed { get; set; } = 0;

        /// <summary>Min width for detail prototype.</summary>
        public float MinWidth { get; set; } = 1f;

        /// <summary>Max width for detail prototype.</summary>
        public float MaxWidth { get; set; } = 2f;

        /// <summary>Min height for detail prototype.</summary>
        public float MinHeight { get; set; } = 0.5f;

        /// <summary>Max height for detail prototype.</summary>
        public float MaxHeight { get; set; } = 1.5f;
    }
}

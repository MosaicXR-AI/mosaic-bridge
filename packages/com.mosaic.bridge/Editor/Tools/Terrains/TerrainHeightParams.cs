using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Terrains
{
    public sealed class TerrainHeightParams
    {
        public int    InstanceId { get; set; }
        public string Name       { get; set; }

        /// <summary>One of: set, flatten, raise-lower, smooth, noise, array.
        /// "array" applies a caller-provided heightmap region in a single
        /// SetHeights call — the right primitive for procedural terrain.</summary>
        [Required] public string Action { get; set; }

        /// <summary>Normalized X position on the terrain (0..1). Brush actions only.</summary>
        public float X { get; set; } = 0.5f;

        /// <summary>Normalized Y position on the terrain (0..1). Brush actions only.</summary>
        public float Y { get; set; } = 0.5f;

        /// <summary>Brush radius in heightmap samples.</summary>
        public int Radius { get; set; } = 10;

        /// <summary>Strength of the operation (0..1).</summary>
        public float Strength { get; set; } = 0.5f;

        /// <summary>Target height for set/flatten actions (normalized 0..1).</summary>
        public float Height { get; set; } = 0f;

        /// <summary>Seed for noise action.</summary>
        public int Seed { get; set; } = 0;

        // --- "array" action params ---

        /// <summary>Integer heightmap X anchor (0..heightmapResolution-1) for the
        /// top-left cell of the provided array. Used only when Action == "array".</summary>
        public int ArrayX { get; set; } = 0;

        /// <summary>Integer heightmap Y anchor for the array. Used only when
        /// Action == "array".</summary>
        public int ArrayY { get; set; } = 0;

        /// <summary>Width of the array region in heightmap cells.
        /// Heights.Length MUST equal Width * Height.</summary>
        public int Width { get; set; } = 0;

        /// <summary>Height of the array region in heightmap cells.</summary>
        public int HeightCells { get; set; } = 0;

        /// <summary>Normalized heights (0..1), row-major: index = row*Width + col.
        /// Values are clamped to [0,1] on apply. Length must equal Width*HeightCells.</summary>
        public float[] Heights { get; set; }

        /// <summary>One of: replace, add, max, min. How to combine the provided
        /// array with existing terrain heights. Default "replace".</summary>
        public string BlendMode { get; set; } = "replace";

        /// <summary>When true, uses SetHeightsDelayLOD to avoid collider rebuild.
        /// Caller must follow up with a sync action (flush on final call).
        /// Default false — each call flushes immediately.</summary>
        public bool DelayLod { get; set; } = false;
    }
}

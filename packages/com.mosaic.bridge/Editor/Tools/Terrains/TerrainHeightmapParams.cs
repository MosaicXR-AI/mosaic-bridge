using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Terrains
{
    public sealed class TerrainHeightmapParams
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }

        /// <summary>Action: import-texture, import-raw, export-raw, get-heights.</summary>
        [Required] public string Action { get; set; }

        /// <summary>import-texture: asset path of a grayscale heightmap Texture2D. Must be
        /// Read/Write Enabled and exactly heightmapResolution x heightmapResolution — no resampling
        /// is performed.</summary>
        public string TexturePath { get; set; }

        /// <summary>import-raw/export-raw: filesystem path to a 16-bit raw heightmap file (NOT a
        /// Unity asset — a real-world DEM/Terrain-Toolbox-style .raw file). Relative paths resolve
        /// against the project root (one level above Assets/).</summary>
        public string RawPath { get; set; }

        /// <summary>Byte order for the 16-bit samples in the raw file: "Little" (Windows/PC
        /// convention, default) or "Big" (Mac convention, matching Unity's own Terrain Toolbox naming).</summary>
        public string ByteOrder { get; set; } = "Little";

        /// <summary>Skip the immediate collider/LOD rebuild after import (caller must flush later).</summary>
        public bool DelayLod { get; set; }
    }
}

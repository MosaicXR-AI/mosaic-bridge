using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Textures
{
    public sealed class TextureSetImportSettingsParams
    {
        /// <summary>Asset path to the texture (e.g., "Assets/Textures/MyTex.png").</summary>
        [Required] public string AssetPath { get; set; }
        /// <summary>Texture type: Default, NormalMap, Sprite, or Editor. Null to leave unchanged.</summary>
        public string TextureType { get; set; }
        /// <summary>Maximum texture size (32, 64, 128, 256, 512, 1024, 2048, 4096, 8192). Null to leave unchanged.</summary>
        public int? MaxSize { get; set; }
        /// <summary>Compression quality: None, LowQuality, NormalQuality, HighQuality. Null to leave unchanged.</summary>
        public string Compression { get; set; }
        /// <summary>Whether the texture uses sRGB color space. Null to leave unchanged.</summary>
        public bool? SRGB { get; set; }
        /// <summary>Filter mode: Point, Bilinear, Trilinear. Null to leave unchanged.</summary>
        public string FilterMode { get; set; }
        /// <summary>Wrap mode: Repeat, Clamp, Mirror, MirrorOnce. Null to leave unchanged.</summary>
        public string WrapMode { get; set; }
        /// <summary>Texture shape: 2D, Cube (cubemap / HDRI), 2DArray, 3D. Null to leave unchanged.
        /// Use "Cube" + TextureType="Default" to convert an equirectangular HDRI into a Cubemap.</summary>
        public string TextureShape { get; set; }

        // ── Sprite params (O4 §4.1, G1) — require TextureType="Sprite" ─────────

        /// <summary>Sprite import mode: "Single" or "Multiple" (multi-sprite sheets — use sprite/slice
        /// afterward to define the individual rects). Null to leave unchanged.</summary>
        public string SpriteMode { get; set; }

        /// <summary>Pixels per world unit. Null to leave unchanged.</summary>
        public float? PixelsPerUnit { get; set; }

        /// <summary>Custom pivot as [x, y] in 0..1 normalized sprite space. Setting this also sets
        /// spriteAlignment to Custom — otherwise Unity ignores spritePivot entirely.</summary>
        public float[] Pivot { get; set; }

        /// <summary>9-slice border as [left, bottom, right, top] pixels. BS:477 — a UI Image's
        /// Sliced type does nothing until this is non-zero; setting Image.type alone is not enough.</summary>
        public float[] Border { get; set; }

        /// <summary>Sprite mesh type: "FullRect" or "Tight". Null to leave unchanged.</summary>
        public string MeshType { get; set; }
    }
}

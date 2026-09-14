#if MOSAIC_HAS_2D_SPRITE
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Sprite2D
{
    public sealed class SpriteSliceParams
    {
        [Required] public string AssetPath { get; set; }

        /// <summary>"Grid" (fixed cell size across the whole texture) or "Explicit" (caller-given
        /// rects). Required.</summary>
        [Required] public string Mode { get; set; }

        // ── Grid mode ────────────────────────────────────────────────────────

        /// <summary>[w, h] in pixels. Required for Grid.</summary>
        public float[] CellSize { get; set; }

        /// <summary>[x, y] pixel offset before the first cell. Default [0, 0].</summary>
        public float[] Offset { get; set; }

        /// <summary>[x, y] pixel gap between cells. Default [0, 0].</summary>
        public float[] Padding { get; set; }

        // ── Explicit mode ────────────────────────────────────────────────────

        /// <summary>Required for Explicit.</summary>
        public SpriteSliceRectParam[] Rects { get; set; }
    }

    public sealed class SpriteSliceRectParam
    {
        [Required] public string Name { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float W { get; set; }
        public float H { get; set; }

        /// <summary>0..1 normalized, relative to this rect. Defaults to [0.5, 0.5] (center).</summary>
        public float[] Pivot { get; set; }
    }
}
#endif

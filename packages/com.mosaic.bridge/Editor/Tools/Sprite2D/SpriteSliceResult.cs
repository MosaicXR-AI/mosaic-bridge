#if MOSAIC_HAS_2D_SPRITE
namespace Mosaic.Bridge.Tools.Sprite2D
{
    public sealed class SpriteSliceResult
    {
        public string AssetPath { get; set; }
        public int SpriteCount { get; set; }
        public SpriteSliceEntry[] Sprites { get; set; }
    }

    public sealed class SpriteSliceEntry
    {
        public string Name { get; set; }

        /// <summary>[x, y, w, h] in texture pixel space (Y-up, origin at bottom-left).</summary>
        public float[] Rect { get; set; }
    }
}
#endif

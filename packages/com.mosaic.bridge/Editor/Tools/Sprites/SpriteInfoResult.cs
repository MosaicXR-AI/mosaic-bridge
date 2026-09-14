namespace Mosaic.Bridge.Tools.Sprites
{
    public sealed class SpriteInfoResult
    {
        public SpriteEntry[] Sprites { get; set; }
    }

    public sealed class SpriteEntry
    {
        /// <summary>The sub-asset name — address it as "AssetPath#Name" for §3.1-aware routes
        /// (component/set_reference, sprite/create, tilemap/create-tile).</summary>
        public string Name { get; set; }
        public float[] Rect { get; set; }
        public float[] Pivot { get; set; }
        public float PixelsPerUnit { get; set; }
        public float[] Border { get; set; }
    }
}

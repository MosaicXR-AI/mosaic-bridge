namespace Mosaic.Bridge.Tools.Sprites
{
    public sealed class SpriteCreateParams
    {
        public string Name { get; set; }
        public string ParentName { get; set; }

        public float[] Position { get; set; }

        /// <summary>Asset path to the sprite, optionally sub-addressed as
        /// "Assets/sheet.png#Run_03" (O4 §3.1). Required.</summary>
        public string SpritePath { get; set; }

        public float[] Color { get; set; }
        public bool FlipX { get; set; }
        public bool FlipY { get; set; }

        /// <summary>"Simple" (default), "Sliced" (needs the sprite's own Border to be non-zero —
        /// see texture/set-import-settings), or "Tiled".</summary>
        public string DrawMode { get; set; }

        /// <summary>[w, h] world size — only meaningful for Sliced/Tiled DrawMode.</summary>
        public float[] Size { get; set; }

        public string SortingLayerName { get; set; }
        public int SortingOrder { get; set; }

        /// <summary>"None" (default), "VisibleInsideMask", "VisibleOutsideMask".</summary>
        public string MaskInteraction { get; set; }

        /// <summary>"None" (default), "Box", "Circle", "Capsule", or "Polygon" (uses the sprite's
        /// own physics shape — set in the Sprite Editor, or falls back to a box).</summary>
        public string AutoCollider { get; set; }
    }
}

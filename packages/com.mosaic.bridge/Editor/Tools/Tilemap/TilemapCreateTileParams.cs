namespace Mosaic.Bridge.Tools.Tilemap
{
    public sealed class TilemapCreateTileParams
    {
        /// <summary>Single mode: one sprite, optionally sub-addressed as 'Assets/sheet.png#Grass'
        /// (O4 §3.1). Required unless SpriteSheetPath (batch mode) is set.</summary>
        public string SpritePath { get; set; }

        /// <summary>Single mode: where to save the Tile asset, e.g. 'Assets/Tiles/Grass.asset'.
        /// Required with SpritePath.</summary>
        public string AssetPath { get; set; }

        /// <summary>Batch mode: a sliced sprite sheet — creates one Tile per sub-sprite, named after
        /// it, saved under OutputFolder. Required unless SpritePath (single mode) is set.</summary>
        public string SpriteSheetPath { get; set; }

        /// <summary>Batch mode: folder to save the generated Tile assets into, e.g. 'Assets/Tiles'.
        /// Required with SpriteSheetPath.</summary>
        public string OutputFolder { get; set; }

        /// <summary>"None" (default), "Sprite" (collider follows the sprite's physics shape), or
        /// "Grid" (the whole cell is solid — the common case for square ground/wall tiles).</summary>
        public string ColliderType { get; set; }

        /// <summary>Tint applied to the tile, [r, g, b, a] 0..1. Defaults to opaque white.</summary>
        public float[] Color { get; set; }
    }
}

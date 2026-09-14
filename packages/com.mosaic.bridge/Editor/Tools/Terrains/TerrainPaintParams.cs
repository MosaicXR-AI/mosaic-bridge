using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Terrains
{
    public sealed class TerrainPaintParams
    {
        public int    InstanceId  { get; set; }
        public string Name        { get; set; }

        [Required] public string Action { get; set; } // add-layer, remove-layer, paint-layer, fill-layer

        /// <summary>Index of the terrain layer to operate on (for paint/fill/remove).</summary>
        public int LayerIndex { get; set; }

        /// <summary>Asset path to a Texture2D for add-layer (e.g. "Assets/Textures/Grass.png").</summary>
        public string TexturePath { get; set; }

        /// <summary>Normal map asset path for add-layer.</summary>
        public string NormalMapPath { get; set; }

        /// <summary>Tile size for add-layer.</summary>
        public float[] TileSize { get; set; } // [x,y] defaults to [15,15]

        /// <summary>add-layer only: explicit asset path for the .terrainlayer. If an asset already
        /// exists there, it is REUSED (added to this terrain, not overwritten) instead of a new
        /// duplicate .terrainlayer being created — the fix for a course's terrain/grid tiles each
        /// minting their own layer asset for what should be one shared layer. Defaults to a path
        /// derived from the texture's own name, so repeated calls with the same TexturePath (the
        /// common terrain/grid case) converge on one asset without this needing to be passed.</summary>
        public string LayerAssetPath { get; set; }

        /// <summary>Normalized X position on the terrain (0..1) for paint.</summary>
        public float X { get; set; } = 0.5f;

        /// <summary>Normalized Y position on the terrain (0..1) for paint.</summary>
        public float Y { get; set; } = 0.5f;

        /// <summary>Brush radius in alphamap samples for paint.</summary>
        public int Radius { get; set; } = 10;

        /// <summary>Paint strength (0..1).</summary>
        public float Strength { get; set; } = 1f;
    }
}

namespace Mosaic.Bridge.Tools.Terrains
{
    public sealed class TerrainSampleHeightResult
    {
        /// <summary>World-space Y at the sampled position.</summary>
        public float WorldY { get; set; }

        /// <summary>Height normalized to [0,1] relative to terrain max height.</summary>
        public float NormalizedHeight { get; set; }

        /// <summary>Name of the terrain that was sampled.</summary>
        public string TerrainName { get; set; }

        /// <summary>Convenience: world position ready to use as a placement coordinate [x, worldY + offset, z].</summary>
        public float[] SuggestedPlacementY { get; set; }

        /// <summary>Terrain world bounds for context.</summary>
        public float[] TerrainSize { get; set; }
    }
}

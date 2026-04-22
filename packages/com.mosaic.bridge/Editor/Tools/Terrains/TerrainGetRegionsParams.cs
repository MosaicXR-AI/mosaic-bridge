namespace Mosaic.Bridge.Tools.Terrains
{
    public sealed class TerrainGetRegionsParams
    {
        /// <summary>Terrain name. Defaults to Terrain.activeTerrain when omitted.</summary>
        public string TerrainName { get; set; }
        public int    InstanceId  { get; set; }

        /// <summary>
        /// Minimum fraction (0..1) of alphamap cells a layer must dominate to be included.
        /// Default 0.01 (1%) — filters out nearly-invisible trace coverage.
        /// </summary>
        public float MinCoverageThreshold { get; set; } = 0.01f;
    }
}

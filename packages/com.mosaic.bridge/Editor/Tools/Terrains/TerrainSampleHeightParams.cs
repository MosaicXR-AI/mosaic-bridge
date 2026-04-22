namespace Mosaic.Bridge.Tools.Terrains
{
    public sealed class TerrainSampleHeightParams
    {
        /// <summary>World-space X position to sample.</summary>
        public float WorldX { get; set; }

        /// <summary>World-space Z position to sample.</summary>
        public float WorldZ { get; set; }

        /// <summary>Terrain name. Defaults to Terrain.activeTerrain when omitted.</summary>
        public string TerrainName { get; set; }

        /// <summary>Terrain instance ID. Preferred over TerrainName when both provided.</summary>
        public int InstanceId { get; set; }
    }
}

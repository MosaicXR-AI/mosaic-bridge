namespace Mosaic.Bridge.Tools.Terrains
{
    public sealed class TerrainGetRegionsResult
    {
        public string          TerrainName   { get; set; }
        public int             InstanceId    { get; set; }
        public int             LayerCount    { get; set; }
        public TerrainRegion[] Regions       { get; set; }
    }

    public sealed class TerrainRegion
    {
        /// <summary>Zero-based layer index into terrainData.terrainLayers.</summary>
        public int    LayerIndex    { get; set; }
        /// <summary>Texture asset path of this layer's diffuse texture (or empty if none).</summary>
        public string TexturePath  { get; set; }
        /// <summary>Fraction of terrain where this layer is dominant (0..1).</summary>
        public float  CoverageFraction { get; set; }
        /// <summary>Coverage as percentage (0..100) for readability.</summary>
        public float  CoveragePercent  { get; set; }

        // World-space bounds of the dominant area
        public float  WorldXMin    { get; set; }
        public float  WorldXMax    { get; set; }
        public float  WorldZMin    { get; set; }
        public float  WorldZMax    { get; set; }

        // Approximate center of dominant area in world space
        public float  CenterWorldX { get; set; }
        public float  CenterWorldZ { get; set; }
    }
}

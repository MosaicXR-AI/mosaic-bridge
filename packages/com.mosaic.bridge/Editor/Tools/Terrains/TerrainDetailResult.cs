namespace Mosaic.Bridge.Tools.Terrains
{
    public sealed class TerrainDetailResult
    {
        public string Action         { get; set; }
        public int    InstanceId     { get; set; }
        public string Name           { get; set; }
        public int    PrototypeCount { get; set; }
        public string Message        { get; set; }

        /// <summary>scatter only: number of detail-map cells actually set (after masked-scatter rejections).</summary>
        public int PlacedCount { get; set; }

        /// <summary>set-resolution only: echoes the resolution applied.</summary>
        public int DetailResolution { get; set; }
        public int ResolutionPerPatch { get; set; }

        /// <summary>scatter-mode only: echoes the mode applied.</summary>
        public string ScatterMode { get; set; }
    }
}

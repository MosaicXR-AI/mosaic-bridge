namespace Mosaic.Bridge.Tools.Terrains
{
    public sealed class TerrainHeightmapResult
    {
        public string Action { get; set; }
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public int Resolution { get; set; }
        public string Message { get; set; }

        /// <summary>get-heights only: flat row-major [Resolution*Resolution] normalized (0..1) heights.</summary>
        public float[] Heights { get; set; }
    }
}

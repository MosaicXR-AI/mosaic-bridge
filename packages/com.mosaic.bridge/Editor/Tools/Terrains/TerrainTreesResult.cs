namespace Mosaic.Bridge.Tools.Terrains
{
    public sealed class TerrainTreeInstanceInfo
    {
        public int PrototypeIndex { get; set; }
        public float[] Position { get; set; }
        public float WidthScale { get; set; }
        public float HeightScale { get; set; }
        public float Rotation { get; set; }
    }

    public sealed class TerrainTreesResult
    {
        public string Action         { get; set; }
        public int    InstanceId     { get; set; }
        public string Name           { get; set; }
        public int    PrototypeCount { get; set; }
        public int    TreeCount      { get; set; }
        public string Message        { get; set; }

        /// <summary>get-instances only: normalized position/rotation/scale per instance.</summary>
        public TerrainTreeInstanceInfo[] Instances { get; set; }

        /// <summary>place: how many candidates were actually placed after masked-scatter rejections.</summary>
        public int PlacedCount { get; set; }
    }
}

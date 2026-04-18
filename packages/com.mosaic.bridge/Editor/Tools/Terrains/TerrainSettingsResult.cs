namespace Mosaic.Bridge.Tools.Terrains
{
    public sealed class TerrainSettingsResult
    {
        public int    InstanceId            { get; set; }
        public string Name                  { get; set; }
        public float  BasemapDistance        { get; set; }
        public float  DetailObjectDistance   { get; set; }
        public float  DetailObjectDensity    { get; set; }
        public float  TreeDistance            { get; set; }
        public float  TreeBillboardDistance   { get; set; }
        public int    TreeMaximumFullLODCount { get; set; }
        public float  HeightmapPixelError    { get; set; }
        public bool   CastShadows            { get; set; }
        public bool   DrawHeightmap          { get; set; }
        public bool   DrawTreesAndFoliage    { get; set; }
        public string Message                { get; set; }
    }
}

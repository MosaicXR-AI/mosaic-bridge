namespace Mosaic.Bridge.Tools.Terrains
{
    public sealed class TerrainGridResult
    {
        public int      Rows       { get; set; }
        public int      Columns    { get; set; }
        public int      TotalTiles { get; set; }
        public int[]    InstanceIds { get; set; }
        public string[] Names      { get; set; }
        public string   Message    { get; set; }
    }
}

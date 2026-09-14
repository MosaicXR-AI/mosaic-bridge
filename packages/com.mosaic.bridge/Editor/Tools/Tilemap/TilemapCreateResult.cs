namespace Mosaic.Bridge.Tools.Tilemap
{
    public sealed class TilemapCreateResult
    {
        public int GridInstanceId { get; set; }
        public string GridName { get; set; }
        public int TilemapInstanceId { get; set; }
        public string TilemapName { get; set; }
        public string CellLayout { get; set; }
        public string SortingLayerName { get; set; }
        public int SortingOrder { get; set; }
    }
}

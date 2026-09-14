namespace Mosaic.Bridge.Tools.Tilemap
{
    public sealed class TilemapSetTilesResult
    {
        public int CellsRequested { get; set; }

        /// <summary>Confirmed via Tilemap.GetTile read-back after painting — never assumed from the
        /// request (O4 §3.5: resolve inside the route, verify by read-back).</summary>
        public int PaintedCount { get; set; }

        public int MinX { get; set; }
        public int MinY { get; set; }
        public int MaxX { get; set; }
        public int MaxY { get; set; }
    }
}

namespace Mosaic.Bridge.Tools.Tilemap
{
    public sealed class TilemapInfoResult
    {
        public string GameObjectName { get; set; }
        public int[] CellBoundsMin { get; set; }
        public int[] CellBoundsMax { get; set; }
        /// <summary>Number of cells with a non-null tile — the metric "≥N ground tiles" means.</summary>
        public int OccupiedCellCount { get; set; }

        /// <summary>Unity's own GetUsedTilesCount — distinct Tile ASSETS referenced, not cells.
        /// A whole floor built from one repeated tile reports 1 here, not the cell count.</summary>
        public int DistinctTileAssetCount { get; set; }
        public string GridCellLayout { get; set; }
        public float[] GridCellSize { get; set; }
    }
}

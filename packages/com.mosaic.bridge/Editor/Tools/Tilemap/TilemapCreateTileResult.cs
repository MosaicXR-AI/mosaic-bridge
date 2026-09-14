namespace Mosaic.Bridge.Tools.Tilemap
{
    public sealed class TilemapCreateTileResult
    {
        /// <summary>Single mode: the one created tile. Batch mode: every tile created, one per sub-sprite.</summary>
        public TileAssetInfo[] Tiles { get; set; }
    }

    public sealed class TileAssetInfo
    {
        public string AssetPath { get; set; }
        public string SpriteName { get; set; }
        public string ColliderType { get; set; }
    }
}

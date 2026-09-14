using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Tilemap
{
    public static class TilemapInfoTool
    {
        // O4 §4.1 (G1): pairs with object/qa-check for "≥N ground tiles", "no floating tiles"
        // style checks — the prerequisite is just being able to ask a Tilemap what it actually
        // contains, which nothing did before this.
        [MosaicTool("tilemap/info",
                    "Reports a Tilemap's used cell bounds, occupied-cell count, distinct-tile-asset count, and " +
                    "the owning Grid's cell layout/size. OccupiedCellCount is the one to use for '≥N ground " +
                    "tiles' checks — Unity's own GetUsedTilesCount (exposed here as DistinctTileAssetCount) " +
                    "counts distinct Tile ASSETS referenced, not painted cells, and returns 1 for a whole floor " +
                    "built from a single repeated tile.",
                    isReadOnly: true)]
        public static ToolResult<TilemapInfoResult> Execute(TilemapInfoParams p)
        {
            if (p.TilemapInstanceId == null && string.IsNullOrEmpty(p.TilemapName))
                return ToolResult<TilemapInfoResult>.Fail(
                    "Either TilemapInstanceId or TilemapName is required", ErrorCodes.INVALID_PARAM);

            GameObject go = null;
#pragma warning disable CS0618
            if (p.TilemapInstanceId.HasValue) go = UnityIds.Resolve(p.TilemapInstanceId.Value) as GameObject;
#pragma warning restore CS0618
            if (go == null && !string.IsNullOrEmpty(p.TilemapName)) go = GameObject.Find(p.TilemapName);
            if (go == null)
                return ToolResult<TilemapInfoResult>.Fail(
                    $"GameObject not found (TilemapInstanceId={p.TilemapInstanceId}, TilemapName='{p.TilemapName}')",
                    ErrorCodes.NOT_FOUND);

            var tilemap = go.GetComponent<UnityEngine.Tilemaps.Tilemap>();
            if (tilemap == null)
                return ToolResult<TilemapInfoResult>.Fail(
                    $"GameObject '{go.name}' has no Tilemap component", ErrorCodes.NOT_FOUND);

            tilemap.CompressBounds();
            var bounds = tilemap.cellBounds;
            var grid = go.GetComponentInParent<Grid>();

            // GetUsedTilesCount() counts distinct Tile ASSETS, not occupied cells — confirmed by a
            // real failing test (two cells painted with the same Tile asset reported 1, not 2).
            // Occupied-cell count, the metric "≥N ground tiles" actually means, has to be counted
            // by hand from the same bounds GetUsedTilesCount is silent about.
            int occupied = 0;
            foreach (var pos in bounds.allPositionsWithin)
                if (tilemap.GetTile(pos) != null) occupied++;

            return ToolResult<TilemapInfoResult>.Ok(new TilemapInfoResult
            {
                GameObjectName = go.name,
                CellBoundsMin = new[] { bounds.xMin, bounds.yMin, bounds.zMin },
                CellBoundsMax = new[] { bounds.xMax, bounds.yMax, bounds.zMax },
                OccupiedCellCount = occupied,
                DistinctTileAssetCount = tilemap.GetUsedTilesCount(),
                GridCellLayout = grid != null ? grid.cellLayout.ToString() : null,
                GridCellSize = grid != null ? new[] { grid.cellSize.x, grid.cellSize.y, grid.cellSize.z } : null,
            });
        }
    }
}

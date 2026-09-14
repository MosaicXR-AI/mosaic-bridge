using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Tilemap
{
    public static class TilemapSetTilesTool
    {
        // O4 §4.1 (G1) objective 4.1 — "a whole level in one call" — and §3.5's own named hazard:
        // a Tile loaded before scene/open can report a painted count that never actually landed on
        // the live Tilemap after the scene swap clears the cell. This resolves every Tile asset
        // fresh, inside the route, and reports PaintedCount from Tilemap.GetTile read-back, never
        // from the request itself.
        [MosaicTool("tilemap/set-tiles",
                    "Paints tiles onto an existing Tilemap. ASCII mode: AsciiMap (rows top-to-bottom, row 0 = " +
                    "highest Y) + Legend (character -> Tile asset path; ' ' and '.' always mean no tile). " +
                    "Cells mode: explicit [{X,Y,TileAssetPath}] list. PaintedCount is read back from the live " +
                    "Tilemap after painting, not assumed from the request.",
                    isReadOnly: false)]
        public static ToolResult<TilemapSetTilesResult> Execute(TilemapSetTilesParams p)
        {
            if (p.TilemapInstanceId == null && string.IsNullOrEmpty(p.TilemapName))
                return ToolResult<TilemapSetTilesResult>.Fail(
                    "Either TilemapInstanceId or TilemapName is required", ErrorCodes.INVALID_PARAM);

            GameObject go = null;
#pragma warning disable CS0618
            if (p.TilemapInstanceId.HasValue) go = UnityIds.Resolve(p.TilemapInstanceId.Value) as GameObject;
#pragma warning restore CS0618
            if (go == null && !string.IsNullOrEmpty(p.TilemapName)) go = GameObject.Find(p.TilemapName);
            if (go == null)
                return ToolResult<TilemapSetTilesResult>.Fail(
                    $"GameObject not found (TilemapInstanceId={p.TilemapInstanceId}, TilemapName='{p.TilemapName}')",
                    ErrorCodes.NOT_FOUND);

            var tilemap = go.GetComponent<UnityEngine.Tilemaps.Tilemap>();
            if (tilemap == null)
                return ToolResult<TilemapSetTilesResult>.Fail(
                    $"GameObject '{go.name}' has no Tilemap component", ErrorCodes.NOT_FOUND);

            bool hasAscii = p.AsciiMap != null;
            bool hasCells = p.Cells != null;
            if (hasAscii == hasCells)
                return ToolResult<TilemapSetTilesResult>.Fail(
                    "Provide either AsciiMap (+ Legend) or Cells — not both, not neither.", ErrorCodes.INVALID_PARAM);

            var cells = new List<(int x, int y, string path)>();
            if (hasAscii)
            {
                if (p.Legend == null)
                    return ToolResult<TilemapSetTilesResult>.Fail("AsciiMap requires Legend", ErrorCodes.INVALID_PARAM);
                for (int row = 0; row < p.AsciiMap.Length; row++)
                {
                    int y = p.AsciiMap.Length - 1 - row;
                    var line = p.AsciiMap[row] ?? "";
                    for (int x = 0; x < line.Length; x++)
                    {
                        var ch = line[x].ToString();
                        if (ch == " " || ch == ".") continue;
                        if (!p.Legend.TryGetValue(ch, out var tilePath))
                            return ToolResult<TilemapSetTilesResult>.Fail(
                                $"AsciiMap character '{ch}' at row {row}, column {x} has no Legend entry.",
                                ErrorCodes.INVALID_PARAM);
                        cells.Add((x, y, tilePath));
                    }
                }
            }
            else
            {
                foreach (var c in p.Cells)
                {
                    if (string.IsNullOrEmpty(c.TileAssetPath))
                        return ToolResult<TilemapSetTilesResult>.Fail(
                            $"Cell ({c.X},{c.Y}) is missing TileAssetPath", ErrorCodes.INVALID_PARAM);
                    cells.Add((c.X, c.Y, c.TileAssetPath));
                }
            }

            // Resolve every distinct tile asset once, fresh, right now — never cached from an
            // earlier call (O4 §3.5).
            var tileCache = new Dictionary<string, TileBase>();
            foreach (var (_, _, path) in cells)
            {
                if (tileCache.ContainsKey(path)) continue;
                var tile = AssetDatabase.LoadAssetAtPath<TileBase>(path);
                if (tile == null)
                    return ToolResult<TilemapSetTilesResult>.Fail($"No Tile asset found at '{path}'", ErrorCodes.NOT_FOUND);
                tileCache[path] = tile;
            }

            if (p.ClearFirst)
                tilemap.ClearAllTiles();

            Undo.RecordObject(tilemap, "Mosaic: Paint Tiles");
            foreach (var (x, y, path) in cells)
                tilemap.SetTile(new Vector3Int(x, y, 0), tileCache[path]);

            int painted = 0;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var (x, y, path) in cells)
            {
                if (tilemap.GetTile(new Vector3Int(x, y, 0)) != null) painted++;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

            return ToolResult<TilemapSetTilesResult>.Ok(new TilemapSetTilesResult
            {
                CellsRequested = cells.Count,
                PaintedCount = painted,
                MinX = cells.Count > 0 ? minX : 0,
                MinY = cells.Count > 0 ? minY : 0,
                MaxX = cells.Count > 0 ? maxX : 0,
                MaxY = cells.Count > 0 ? maxY : 0,
            });
        }
    }
}

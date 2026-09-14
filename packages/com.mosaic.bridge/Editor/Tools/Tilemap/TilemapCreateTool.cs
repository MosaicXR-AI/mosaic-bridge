using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Tilemap
{
    public static class TilemapCreateTool
    {
        // O4 §4.1 (G1): zero 2D routes existed before this — "GameObject/2D Object/Tilemap/
        // Rectangular" via editor/execute-menu-item was the only interim, and it reports back no
        // name/parent/id. A Day-2 course needs Ground/Background/Hazards as three layers sharing
        // one Grid, which is why reusing an existing Grid (GridName/GridInstanceId) is the primary
        // path here, not an afterthought.
        [MosaicTool("tilemap/create",
                    "Creates a Tilemap layer (Grid + Tilemap + TilemapRenderer). Reuses an existing Grid via " +
                    "GridName/GridInstanceId when adding a second/third layer (Background, Hazards) — layers on " +
                    "the same Grid share cell size and layout; grid-only params (CellLayout/CellSize/CellSwizzle) " +
                    "are ignored when reusing one. CellLayout: Rectangular (default), Hexagon, Isometric, " +
                    "IsometricZAsY — the latter two need a matching CellSwizzle.",
                    isReadOnly: false)]
        public static ToolResult<TilemapCreateResult> Execute(TilemapCreateParams p)
        {
            GameObject gridGo = null;
#pragma warning disable CS0618
            if (p.GridInstanceId.HasValue) gridGo = UnityIds.Resolve(p.GridInstanceId.Value) as GameObject;
#pragma warning restore CS0618
            if (gridGo == null && !string.IsNullOrEmpty(p.GridName)) gridGo = GameObject.Find(p.GridName);

            Grid grid;
            if (gridGo != null)
            {
                grid = gridGo.GetComponent<Grid>();
                if (grid == null)
                    return ToolResult<TilemapCreateResult>.Fail(
                        $"GameObject '{gridGo.name}' has no Grid component", ErrorCodes.INVALID_PARAM);
            }
            else
            {
                gridGo = new GameObject(string.IsNullOrEmpty(p.GridName) ? "Grid" : p.GridName);
                grid = gridGo.AddComponent<Grid>();

                if (!string.IsNullOrEmpty(p.CellLayout))
                {
                    if (!TryParseCellLayout(p.CellLayout, out var layout))
                        return ToolResult<TilemapCreateResult>.Fail(
                            $"Unknown CellLayout '{p.CellLayout}'. Valid: Rectangular, Hexagon, Isometric, IsometricZAsY",
                            ErrorCodes.INVALID_PARAM);
                    grid.cellLayout = layout;
                }
                if (p.CellSize != null)
                {
                    if (p.CellSize.Length != 3)
                        return ToolResult<TilemapCreateResult>.Fail("CellSize requires exactly [x, y, z]", ErrorCodes.INVALID_PARAM);
                    grid.cellSize = new Vector3(p.CellSize[0], p.CellSize[1], p.CellSize[2]);
                }
                if (!string.IsNullOrEmpty(p.CellSwizzle))
                {
                    if (!TryParseCellSwizzle(p.CellSwizzle, out var swizzle))
                        return ToolResult<TilemapCreateResult>.Fail(
                            $"Unknown CellSwizzle '{p.CellSwizzle}'. Valid: XYZ, XZY, YXZ, YZX, ZXY, ZYX",
                            ErrorCodes.INVALID_PARAM);
                    grid.cellSwizzle = swizzle;
                }
                Undo.RegisterCreatedObjectUndo(gridGo, "Mosaic: Create Grid");
            }

            var layerGo = new GameObject(string.IsNullOrEmpty(p.Name) ? "Tilemap" : p.Name);
            layerGo.transform.SetParent(gridGo.transform, false);
            layerGo.AddComponent<UnityEngine.Tilemaps.Tilemap>();
            var renderer = layerGo.AddComponent<TilemapRenderer>();

            if (!string.IsNullOrEmpty(p.SortingLayerName))
                renderer.sortingLayerName = p.SortingLayerName;
            renderer.sortingOrder = p.SortingOrder;
            if (!string.IsNullOrEmpty(p.Mode))
            {
                if (!System.Enum.TryParse(p.Mode, true, out TilemapRenderer.Mode mode))
                    return ToolResult<TilemapCreateResult>.Fail(
                        $"Unknown Mode '{p.Mode}'. Valid: Chunk, Individual", ErrorCodes.INVALID_PARAM);
                renderer.mode = mode;
            }
            Undo.RegisterCreatedObjectUndo(layerGo, "Mosaic: Create Tilemap Layer");

            return ToolResult<TilemapCreateResult>.Ok(new TilemapCreateResult
            {
                GridInstanceId = UnityIds.Of(gridGo),
                GridName = gridGo.name,
                TilemapInstanceId = UnityIds.Of(layerGo),
                TilemapName = layerGo.name,
                CellLayout = grid.cellLayout.ToString(),
                SortingLayerName = renderer.sortingLayerName,
                SortingOrder = renderer.sortingOrder,
            });
        }

        private static bool TryParseCellLayout(string value, out GridLayout.CellLayout result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "rectangular":     result = GridLayout.CellLayout.Rectangle;      return true;
                case "hexagon":         result = GridLayout.CellLayout.Hexagon;        return true;
                case "isometric":       result = GridLayout.CellLayout.Isometric;      return true;
                case "isometriczasy":   result = GridLayout.CellLayout.IsometricZAsY;  return true;
                default:                result = GridLayout.CellLayout.Rectangle;      return false;
            }
        }

        private static bool TryParseCellSwizzle(string value, out GridLayout.CellSwizzle result)
        {
            switch (value?.Trim().ToUpperInvariant())
            {
                case "XYZ": result = GridLayout.CellSwizzle.XYZ; return true;
                case "XZY": result = GridLayout.CellSwizzle.XZY; return true;
                case "YXZ": result = GridLayout.CellSwizzle.YXZ; return true;
                case "YZX": result = GridLayout.CellSwizzle.YZX; return true;
                case "ZXY": result = GridLayout.CellSwizzle.ZXY; return true;
                case "ZYX": result = GridLayout.CellSwizzle.ZYX; return true;
                default:    result = GridLayout.CellSwizzle.XYZ; return false;
            }
        }
    }
}

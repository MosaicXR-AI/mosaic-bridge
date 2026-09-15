using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Terrains
{
    public static class TerrainNeighborsTool
    {
        [MosaicTool("terrain/neighbors",
                    "Sets Terrain.SetNeighbors (Left=-X, Top=+Z, Right=+X, Bottom=-Z) so LOD " +
                    "transitions align across tile boundaries — for stitching individually " +
                    "created/imported terrains. Omit a side's name to leave it unset (null).",
                    isReadOnly: false)]
        public static ToolResult<TerrainNeighborsResult> Execute(TerrainNeighborsParams p)
        {
            var terrain = TerrainToolHelpers.ResolveTerrain(p.InstanceId, p.Name, out string error);
            if (terrain == null)
                return ToolResult<TerrainNeighborsResult>.Fail(error, ErrorCodes.NOT_FOUND);

            if (!TryResolveOptional(p.LeftName, out var left, out var leftError))
                return ToolResult<TerrainNeighborsResult>.Fail(leftError, ErrorCodes.NOT_FOUND);
            if (!TryResolveOptional(p.TopName, out var top, out var topError))
                return ToolResult<TerrainNeighborsResult>.Fail(topError, ErrorCodes.NOT_FOUND);
            if (!TryResolveOptional(p.RightName, out var right, out var rightError))
                return ToolResult<TerrainNeighborsResult>.Fail(rightError, ErrorCodes.NOT_FOUND);
            if (!TryResolveOptional(p.BottomName, out var bottom, out var bottomError))
                return ToolResult<TerrainNeighborsResult>.Fail(bottomError, ErrorCodes.NOT_FOUND);

            Undo.RecordObject(terrain, "Mosaic: Set Terrain Neighbors");
            terrain.SetNeighbors(left, top, right, bottom);
            EditorUtility.SetDirty(terrain);

            return ToolResult<TerrainNeighborsResult>.Ok(new TerrainNeighborsResult
            {
                InstanceId = UnityIds.Of(terrain.gameObject),
                Name = terrain.gameObject.name,
                Left = left?.gameObject.name,
                Top = top?.gameObject.name,
                Right = right?.gameObject.name,
                Bottom = bottom?.gameObject.name,
            });
        }

        private static bool TryResolveOptional(string name, out UnityEngine.Terrain terrain, out string error)
        {
            terrain = null;
            error = null;
            if (string.IsNullOrEmpty(name))
                return true;
            terrain = TerrainToolHelpers.ResolveTerrain(0, name, out error);
            return terrain != null;
        }
    }
}

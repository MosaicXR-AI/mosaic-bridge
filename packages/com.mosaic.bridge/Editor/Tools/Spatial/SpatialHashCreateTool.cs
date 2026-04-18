using System.Collections.Generic;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Spatial
{
    public static class SpatialHashCreateTool
    {
        [MosaicTool("spatial/hash-create",
                    "Creates a spatial hash grid from points and/or GameObject positions for fast neighbor queries",
                    isReadOnly: false, category: "spatial", Context = ToolContext.Both)]
        public static ToolResult<SpatialHashCreateResult> Execute(SpatialHashCreateParams p)
        {
            if (string.IsNullOrEmpty(p.StructureId))
                return ToolResult<SpatialHashCreateResult>.Fail(
                    "StructureId is required", ErrorCodes.INVALID_PARAM);

            if (p.CellSize <= 0f)
                return ToolResult<SpatialHashCreateResult>.Fail(
                    "CellSize must be greater than zero", ErrorCodes.INVALID_PARAM);

            int dims = p.Dimensions == 0 ? 3 : p.Dimensions;
            if (dims < 2 || dims > 3)
                return ToolResult<SpatialHashCreateResult>.Fail(
                    "Dimensions must be 2 or 3", ErrorCodes.INVALID_PARAM);

            var grid = new SpatialHashGrid
            {
                StructureId = p.StructureId,
                CellSize    = p.CellSize,
                Dimensions  = dims
            };

            // ---- Insert points from parameter list ----
            if (p.Points != null)
            {
                for (int i = 0; i < p.Points.Count; i++)
                {
                    var pt = p.Points[i];
                    if (pt == null)
                        return ToolResult<SpatialHashCreateResult>.Fail(
                            $"Point at index {i} is null", ErrorCodes.INVALID_PARAM);
                    if (pt.Position == null || pt.Position.Length < dims)
                        return ToolResult<SpatialHashCreateResult>.Fail(
                            $"Point '{pt.Id}' Position must have at least {dims} components",
                            ErrorCodes.INVALID_PARAM);

                    // Normalize position to 3 floats so query logic works uniformly.
                    var pos = new float[3];
                    pos[0] = pt.Position[0];
                    pos[1] = pt.Position[1];
                    pos[2] = pt.Position.Length >= 3 ? pt.Position[2] : 0f;

                    grid.Insert(new SpatialHashPoint
                    {
                        Id       = string.IsNullOrEmpty(pt.Id) ? $"point_{i}" : pt.Id,
                        Position = pos,
                        Data     = pt.Data
                    });
                }
            }

            // ---- Insert GameObject positions ----
            if (p.GameObjects != null)
            {
                foreach (var name in p.GameObjects)
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    var go = GameObject.Find(name);
                    if (go == null)
                        return ToolResult<SpatialHashCreateResult>.Fail(
                            $"GameObject '{name}' not found", ErrorCodes.NOT_FOUND);

                    Vector3 wp = go.transform.position;
                    grid.Insert(new SpatialHashPoint
                    {
                        Id       = go.name,
                        Position = new[] { wp.x, wp.y, wp.z },
                        Data     = null
                    });
                }
            }

            SpatialHashGrid.Registry[p.StructureId] = grid;

            return ToolResult<SpatialHashCreateResult>.Ok(new SpatialHashCreateResult
            {
                StructureId     = p.StructureId,
                Dimensions      = dims,
                CellSize        = p.CellSize,
                PointCount      = grid.PointCount,
                CellCount       = grid.Cells.Count,
                MaxPointsInCell = grid.MaxPointsInCell()
            });
        }
    }
}

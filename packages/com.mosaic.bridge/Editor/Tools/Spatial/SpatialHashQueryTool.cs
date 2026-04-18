using System.Collections.Generic;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Spatial
{
    public static class SpatialHashQueryTool
    {
        [MosaicTool("spatial/hash-query",
                    "Queries a spatial hash grid by radius, cell, or axis-aligned range",
                    isReadOnly: true, category: "spatial", Context = ToolContext.Both)]
        public static ToolResult<SpatialHashQueryResult> Execute(SpatialHashQueryParams p)
        {
            if (string.IsNullOrEmpty(p.StructureId))
                return ToolResult<SpatialHashQueryResult>.Fail(
                    "StructureId is required", ErrorCodes.INVALID_PARAM);

            if (!SpatialHashGrid.Registry.TryGetValue(p.StructureId, out var grid))
                return ToolResult<SpatialHashQueryResult>.Fail(
                    $"Spatial hash structure '{p.StructureId}' not found", ErrorCodes.NOT_FOUND);

            if (string.IsNullOrEmpty(p.QueryType))
                return ToolResult<SpatialHashQueryResult>.Fail(
                    "QueryType is required (radius|cell|range)", ErrorCodes.INVALID_PARAM);

            string qt = p.QueryType.ToLowerInvariant();
            var hits = new List<SpatialHashQueryHit>();
            int cellsVisited = 0;

            switch (qt)
            {
                case "radius":
                {
                    if (p.Position == null || p.Position.Length < grid.Dimensions)
                        return ToolResult<SpatialHashQueryResult>.Fail(
                            $"Position must have at least {grid.Dimensions} components for radius query",
                            ErrorCodes.INVALID_PARAM);
                    if (p.Radius <= 0f)
                        return ToolResult<SpatialHashQueryResult>.Fail(
                            "Radius must be greater than zero", ErrorCodes.INVALID_PARAM);

                    float px = p.Position[0];
                    float py = p.Position[1];
                    float pz = grid.Dimensions == 3 && p.Position.Length >= 3 ? p.Position[2] : 0f;
                    float r  = p.Radius;
                    float r2 = r * r;

                    int minX = grid.CellX(px - r);
                    int maxX = grid.CellX(px + r);
                    int minY = grid.CellY(py - r);
                    int maxY = grid.CellY(py + r);
                    int minZ = grid.Dimensions == 3 ? grid.CellZ(pz - r) : 0;
                    int maxZ = grid.Dimensions == 3 ? grid.CellZ(pz + r) : 0;

                    for (int cx = minX; cx <= maxX; cx++)
                    for (int cy = minY; cy <= maxY; cy++)
                    for (int cz = minZ; cz <= maxZ; cz++)
                    {
                        long key = grid.HashCell(cx, cy, cz);
                        cellsVisited++;
                        if (!grid.Cells.TryGetValue(key, out var bucket)) continue;
                        foreach (var pt in bucket)
                        {
                            float dx = pt.Position[0] - px;
                            float dy = pt.Position[1] - py;
                            float dz = grid.Dimensions == 3 ? pt.Position[2] - pz : 0f;
                            float d2 = dx * dx + dy * dy + dz * dz;
                            if (d2 <= r2)
                            {
                                hits.Add(new SpatialHashQueryHit
                                {
                                    Id       = pt.Id,
                                    Position = pt.Position,
                                    Data     = pt.Data,
                                    Distance = Mathf.Sqrt(d2)
                                });
                            }
                        }
                    }
                    break;
                }

                case "cell":
                {
                    if (p.CellCoord == null || p.CellCoord.Length < grid.Dimensions)
                        return ToolResult<SpatialHashQueryResult>.Fail(
                            $"CellCoord must have at least {grid.Dimensions} components for cell query",
                            ErrorCodes.INVALID_PARAM);

                    int cx = p.CellCoord[0];
                    int cy = p.CellCoord[1];
                    int cz = grid.Dimensions == 3 && p.CellCoord.Length >= 3 ? p.CellCoord[2] : 0;
                    long key = grid.HashCell(cx, cy, cz);
                    cellsVisited = 1;
                    if (grid.Cells.TryGetValue(key, out var bucket))
                    {
                        foreach (var pt in bucket)
                        {
                            hits.Add(new SpatialHashQueryHit
                            {
                                Id       = pt.Id,
                                Position = pt.Position,
                                Data     = pt.Data,
                                Distance = 0f
                            });
                        }
                    }
                    break;
                }

                case "range":
                {
                    if (p.RangeMin == null || p.RangeMax == null ||
                        p.RangeMin.Length < grid.Dimensions || p.RangeMax.Length < grid.Dimensions)
                        return ToolResult<SpatialHashQueryResult>.Fail(
                            $"RangeMin and RangeMax must have at least {grid.Dimensions} components",
                            ErrorCodes.INVALID_PARAM);

                    float minPx = p.RangeMin[0];
                    float minPy = p.RangeMin[1];
                    float minPz = grid.Dimensions == 3 && p.RangeMin.Length >= 3 ? p.RangeMin[2] : 0f;
                    float maxPx = p.RangeMax[0];
                    float maxPy = p.RangeMax[1];
                    float maxPz = grid.Dimensions == 3 && p.RangeMax.Length >= 3 ? p.RangeMax[2] : 0f;

                    if (maxPx < minPx || maxPy < minPy || (grid.Dimensions == 3 && maxPz < minPz))
                        return ToolResult<SpatialHashQueryResult>.Fail(
                            "RangeMax must be >= RangeMin in every axis", ErrorCodes.INVALID_PARAM);

                    int minX = grid.CellX(minPx);
                    int maxX = grid.CellX(maxPx);
                    int minY = grid.CellY(minPy);
                    int maxY = grid.CellY(maxPy);
                    int minZ = grid.Dimensions == 3 ? grid.CellZ(minPz) : 0;
                    int maxZ = grid.Dimensions == 3 ? grid.CellZ(maxPz) : 0;

                    for (int cx = minX; cx <= maxX; cx++)
                    for (int cy = minY; cy <= maxY; cy++)
                    for (int cz = minZ; cz <= maxZ; cz++)
                    {
                        long key = grid.HashCell(cx, cy, cz);
                        cellsVisited++;
                        if (!grid.Cells.TryGetValue(key, out var bucket)) continue;
                        foreach (var pt in bucket)
                        {
                            float x = pt.Position[0];
                            float y = pt.Position[1];
                            float z = grid.Dimensions == 3 ? pt.Position[2] : 0f;
                            if (x < minPx || x > maxPx) continue;
                            if (y < minPy || y > maxPy) continue;
                            if (grid.Dimensions == 3 && (z < minPz || z > maxPz)) continue;
                            hits.Add(new SpatialHashQueryHit
                            {
                                Id       = pt.Id,
                                Position = pt.Position,
                                Data     = pt.Data,
                                Distance = 0f
                            });
                        }
                    }
                    break;
                }

                default:
                    return ToolResult<SpatialHashQueryResult>.Fail(
                        $"Unknown QueryType '{p.QueryType}' (expected radius|cell|range)",
                        ErrorCodes.INVALID_PARAM);
            }

            return ToolResult<SpatialHashQueryResult>.Ok(new SpatialHashQueryResult
            {
                StructureId  = p.StructureId,
                QueryType    = qt,
                Points       = hits,
                Count        = hits.Count,
                CellsVisited = cellsVisited
            });
        }
    }
}

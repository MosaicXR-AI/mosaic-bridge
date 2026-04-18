using System.Collections.Generic;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Spatial
{
    public static class SpatialOctreeCreateTool
    {
        [MosaicTool("spatial/octree-create",
                    "Creates an octree (3D) or quadtree (2D) spatial data structure from a list of points and/or scene GameObjects for later range/nearest/knn/radius queries",
                    isReadOnly: false, category: "spatial", Context = ToolContext.Both)]
        public static ToolResult<SpatialOctreeCreateResult> Execute(SpatialOctreeCreateParams p)
        {
            // ── Validate params ────────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(p.StructureId))
                return ToolResult<SpatialOctreeCreateResult>.Fail(
                    "StructureId is required", ErrorCodes.INVALID_PARAM);

            if (p.Dimensions != 2 && p.Dimensions != 3)
                return ToolResult<SpatialOctreeCreateResult>.Fail(
                    "Dimensions must be 2 (quadtree) or 3 (octree)", ErrorCodes.INVALID_PARAM);

            if (p.BoundsMin == null || p.BoundsMin.Length != 3)
                return ToolResult<SpatialOctreeCreateResult>.Fail(
                    "BoundsMin must be an array of 3 floats [x, y, z]", ErrorCodes.INVALID_PARAM);

            if (p.BoundsMax == null || p.BoundsMax.Length != 3)
                return ToolResult<SpatialOctreeCreateResult>.Fail(
                    "BoundsMax must be an array of 3 floats [x, y, z]", ErrorCodes.INVALID_PARAM);

            var bMin = new Vector3(p.BoundsMin[0], p.BoundsMin[1], p.BoundsMin[2]);
            var bMax = new Vector3(p.BoundsMax[0], p.BoundsMax[1], p.BoundsMax[2]);

            if (bMax.x <= bMin.x || bMax.y <= bMin.y ||
                (p.Dimensions == 3 && bMax.z <= bMin.z))
                return ToolResult<SpatialOctreeCreateResult>.Fail(
                    "BoundsMax must be strictly greater than BoundsMin on each active axis",
                    ErrorCodes.INVALID_PARAM);

            if (p.MaxDepth <= 0)
                return ToolResult<SpatialOctreeCreateResult>.Fail(
                    "MaxDepth must be > 0", ErrorCodes.INVALID_PARAM);

            if (p.MaxPointsPerNode <= 0)
                return ToolResult<SpatialOctreeCreateResult>.Fail(
                    "MaxPointsPerNode must be > 0", ErrorCodes.INVALID_PARAM);

            // ── Build structure ────────────────────────────────────────────
            var structure = new OctreeStructure
            {
                Dimensions       = p.Dimensions,
                MaxDepth         = p.MaxDepth,
                MaxPointsPerNode = p.MaxPointsPerNode,
                Root             = new OctreeNode(bMin, bMax, 0, p.Dimensions),
                NodeCount        = 1,
                MaxDepthReached  = 0,
                PointCount       = 0
            };

            // ── Gather points ──────────────────────────────────────────────
            var allPoints = new List<OctreePoint>();

            if (p.Points != null)
            {
                for (int i = 0; i < p.Points.Count; i++)
                {
                    var src = p.Points[i];
                    if (src == null) continue;
                    if (src.Position == null || src.Position.Length != 3)
                        return ToolResult<SpatialOctreeCreateResult>.Fail(
                            $"Point[{i}].Position must be an array of 3 floats",
                            ErrorCodes.INVALID_PARAM);

                    var pos = new Vector3(src.Position[0], src.Position[1], src.Position[2]);
                    if (!OctreeStore.Contains(structure.Root, pos))
                        return ToolResult<SpatialOctreeCreateResult>.Fail(
                            $"Point[{i}] ({pos.x},{pos.y},{pos.z}) is outside the structure bounds",
                            ErrorCodes.OUT_OF_RANGE);

                    allPoints.Add(new OctreePoint
                    {
                        Id       = string.IsNullOrEmpty(src.Id) ? $"pt_{i}" : src.Id,
                        Position = pos,
                        Data     = src.Data
                    });
                }
            }

            if (p.GameObjects != null)
            {
                for (int i = 0; i < p.GameObjects.Length; i++)
                {
                    var name = p.GameObjects[i];
                    if (string.IsNullOrEmpty(name)) continue;
                    var go = GameObject.Find(name);
                    if (go == null)
                        return ToolResult<SpatialOctreeCreateResult>.Fail(
                            $"GameObject '{name}' not found in scene",
                            ErrorCodes.NOT_FOUND);

                    var pos = go.transform.position;
                    if (!OctreeStore.Contains(structure.Root, pos))
                        return ToolResult<SpatialOctreeCreateResult>.Fail(
                            $"GameObject '{name}' position is outside the structure bounds",
                            ErrorCodes.OUT_OF_RANGE);

                    allPoints.Add(new OctreePoint
                    {
                        Id       = name,
                        Position = pos,
                        Data     = null
                    });
                }
            }

            // ── Insert into octree ─────────────────────────────────────────
            foreach (var pt in allPoints)
                OctreeStore.Insert(structure, pt);

            OctreeStore.Put(p.StructureId, structure);

            return ToolResult<SpatialOctreeCreateResult>.Ok(new SpatialOctreeCreateResult
            {
                StructureId     = p.StructureId,
                Dimensions      = structure.Dimensions,
                PointCount      = structure.PointCount,
                NodeCount       = structure.NodeCount,
                MaxDepthReached = structure.MaxDepthReached
            });
        }
    }
}

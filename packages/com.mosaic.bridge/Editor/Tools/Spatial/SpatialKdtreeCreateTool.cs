using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Spatial
{
    public static class SpatialKdtreeCreateTool
    {
        [MosaicTool("spatial/kdtree-create",
                    "Builds a KD-tree over a set of k-dimensional points (k = 1..4) for fast nearest / KNN / radius / range queries",
                    isReadOnly: false, category: "spatial", Context = ToolContext.Both)]
        public static ToolResult<SpatialKdtreeCreateResult> Execute(SpatialKdtreeCreateParams p)
        {
            if (p == null)
                return ToolResult<SpatialKdtreeCreateResult>.Fail(
                    "Params are required", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrEmpty(p.StructureId))
                return ToolResult<SpatialKdtreeCreateResult>.Fail(
                    "StructureId is required", ErrorCodes.INVALID_PARAM);

            int dim = p.Dimensions;
            if (dim < 1 || dim > 4)
                return ToolResult<SpatialKdtreeCreateResult>.Fail(
                    "Dimensions must be between 1 and 4", ErrorCodes.OUT_OF_RANGE);

            // ── Collect points ──────────────────────────────────────────
            var pts = new List<SpatialKdtreeStore.StoredPoint>();

            if (p.Points != null)
            {
                for (int i = 0; i < p.Points.Count; i++)
                {
                    var src = p.Points[i];
                    if (src == null)
                        return ToolResult<SpatialKdtreeCreateResult>.Fail(
                            $"Points[{i}] is null", ErrorCodes.INVALID_PARAM);
                    if (src.Position == null || src.Position.Length != dim)
                        return ToolResult<SpatialKdtreeCreateResult>.Fail(
                            $"Points[{i}].Position must have exactly {dim} components",
                            ErrorCodes.INVALID_PARAM);

                    var pos = new float[dim];
                    Array.Copy(src.Position, pos, dim);

                    pts.Add(new SpatialKdtreeStore.StoredPoint
                    {
                        Id       = string.IsNullOrEmpty(src.Id) ? $"p{i}" : src.Id,
                        Position = pos,
                        Data     = src.Data
                    });
                }
            }

            if (p.GameObjects != null && p.GameObjects.Length > 0)
            {
                if (dim != 3)
                    return ToolResult<SpatialKdtreeCreateResult>.Fail(
                        "GameObjects can only be used when Dimensions = 3",
                        ErrorCodes.INVALID_PARAM);

                for (int i = 0; i < p.GameObjects.Length; i++)
                {
                    var name = p.GameObjects[i];
                    if (string.IsNullOrEmpty(name))
                        continue;
                    var go = GameObject.Find(name);
                    if (go == null)
                        return ToolResult<SpatialKdtreeCreateResult>.Fail(
                            $"GameObject '{name}' not found", ErrorCodes.NOT_FOUND);

                    var wp = go.transform.position;
                    pts.Add(new SpatialKdtreeStore.StoredPoint
                    {
                        Id       = name,
                        Position = new[] { wp.x, wp.y, wp.z },
                        Data     = null
                    });
                }
            }

            if (pts.Count == 0)
                return ToolResult<SpatialKdtreeCreateResult>.Fail(
                    "At least one point is required (via Points or GameObjects)",
                    ErrorCodes.INVALID_PARAM);

            // ── Build tree ──────────────────────────────────────────────
            var sw = Stopwatch.StartNew();
            int maxDepth = 0;
            var root = Build(pts, 0, dim, ref maxDepth);
            sw.Stop();

            var tree = new SpatialKdtreeStore.KDTree
            {
                Dimensions = dim,
                PointCount = pts.Count,
                Depth      = maxDepth,
                Root       = root
            };
            SpatialKdtreeStore.Trees[p.StructureId] = tree;

            return ToolResult<SpatialKdtreeCreateResult>.Ok(new SpatialKdtreeCreateResult
            {
                StructureId = p.StructureId,
                Dimensions  = dim,
                PointCount  = pts.Count,
                TreeDepth   = maxDepth,
                BuildTimeMs = sw.ElapsedMilliseconds
            });
        }

        // ─── Recursive median-split build ───────────────────────────────
        static SpatialKdtreeStore.KDNode Build(
            List<SpatialKdtreeStore.StoredPoint> items,
            int depth, int dim, ref int maxDepth)
        {
            if (items == null || items.Count == 0) return null;
            if (depth + 1 > maxDepth) maxDepth = depth + 1;

            int axis = depth % dim;
            items.Sort((a, b) => a.Position[axis].CompareTo(b.Position[axis]));
            int median = items.Count / 2;

            var node = new SpatialKdtreeStore.KDNode
            {
                Point = items[median],
                Axis  = axis
            };

            var leftSlice  = items.GetRange(0, median);
            var rightSlice = items.GetRange(median + 1, items.Count - median - 1);

            node.Left  = Build(leftSlice,  depth + 1, dim, ref maxDepth);
            node.Right = Build(rightSlice, depth + 1, dim, ref maxDepth);
            return node;
        }
    }
}

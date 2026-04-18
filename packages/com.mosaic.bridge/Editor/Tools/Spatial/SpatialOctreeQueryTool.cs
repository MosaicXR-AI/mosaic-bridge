using System;
using System.Collections.Generic;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Spatial
{
    public static class SpatialOctreeQueryTool
    {
        [MosaicTool("spatial/octree-query",
                    "Queries an existing octree/quadtree: range (AABB), nearest, knn, or radius (sphere). Returns matching points and a NodesVisited performance metric",
                    isReadOnly: true, category: "spatial", Context = ToolContext.Both)]
        public static ToolResult<SpatialOctreeQueryResult> Execute(SpatialOctreeQueryParams p)
        {
            if (string.IsNullOrWhiteSpace(p.StructureId))
                return ToolResult<SpatialOctreeQueryResult>.Fail(
                    "StructureId is required", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrWhiteSpace(p.QueryType))
                return ToolResult<SpatialOctreeQueryResult>.Fail(
                    "QueryType is required (range | nearest | knn | radius)",
                    ErrorCodes.INVALID_PARAM);

            if (!OctreeStore.TryGet(p.StructureId, out var s))
                return ToolResult<SpatialOctreeQueryResult>.Fail(
                    $"Structure '{p.StructureId}' not found. Call spatial/octree-create first.",
                    ErrorCodes.NOT_FOUND);

            string qt  = p.QueryType.ToLowerInvariant();
            int    dims = s.Dimensions;
            int    visited = 0;

            switch (qt)
            {
                case "range":
                {
                    if (p.Range == null || p.Range.Length != 2 ||
                        p.Range[0] == null || p.Range[0].Length != 3 ||
                        p.Range[1] == null || p.Range[1].Length != 3)
                        return ToolResult<SpatialOctreeQueryResult>.Fail(
                            "Range must be [[minX,minY,minZ],[maxX,maxY,maxZ]]",
                            ErrorCodes.INVALID_PARAM);

                    var rMin = new Vector3(p.Range[0][0], p.Range[0][1], p.Range[0][2]);
                    var rMax = new Vector3(p.Range[1][0], p.Range[1][1], p.Range[1][2]);
                    var found = new List<OctreePoint>();
                    RangeSearch(s.Root, rMin, rMax, dims, found, ref visited);

                    var points = new List<SpatialOctreeQueryResult.QueryPoint>(found.Count);
                    foreach (var pt in found)
                        points.Add(ToQueryPoint(pt, 0f));

                    return ToolResult<SpatialOctreeQueryResult>.Ok(new SpatialOctreeQueryResult
                    {
                        StructureId  = p.StructureId,
                        QueryType    = "range",
                        Points       = points,
                        Count        = points.Count,
                        NodesVisited = visited
                    });
                }

                case "radius":
                {
                    if (p.Position == null || p.Position.Length != 3)
                        return ToolResult<SpatialOctreeQueryResult>.Fail(
                            "Position (float[3]) is required for radius query",
                            ErrorCodes.INVALID_PARAM);
                    if (p.Radius < 0f)
                        return ToolResult<SpatialOctreeQueryResult>.Fail(
                            "Radius must be >= 0", ErrorCodes.INVALID_PARAM);

                    var center = new Vector3(p.Position[0], p.Position[1], p.Position[2]);
                    float rSq  = p.Radius * p.Radius;
                    var found  = new List<(OctreePoint pt, float distSq)>();
                    RadiusSearch(s.Root, center, rSq, dims, found, ref visited);
                    found.Sort((a, b) => a.distSq.CompareTo(b.distSq));

                    var points = new List<SpatialOctreeQueryResult.QueryPoint>(found.Count);
                    foreach (var f in found)
                        points.Add(ToQueryPoint(f.pt, Mathf.Sqrt(f.distSq)));

                    return ToolResult<SpatialOctreeQueryResult>.Ok(new SpatialOctreeQueryResult
                    {
                        StructureId  = p.StructureId,
                        QueryType    = "radius",
                        Points       = points,
                        Count        = points.Count,
                        NodesVisited = visited
                    });
                }

                case "nearest":
                case "knn":
                {
                    if (p.Position == null || p.Position.Length != 3)
                        return ToolResult<SpatialOctreeQueryResult>.Fail(
                            "Position (float[3]) is required for nearest/knn query",
                            ErrorCodes.INVALID_PARAM);

                    int k = qt == "nearest" ? 1 : Math.Max(1, p.K);
                    var center = new Vector3(p.Position[0], p.Position[1], p.Position[2]);

                    var best = KnnSearch(s.Root, center, k, dims, ref visited);

                    var points = new List<SpatialOctreeQueryResult.QueryPoint>(best.Count);
                    foreach (var f in best)
                        points.Add(ToQueryPoint(f.pt, Mathf.Sqrt(f.distSq)));

                    return ToolResult<SpatialOctreeQueryResult>.Ok(new SpatialOctreeQueryResult
                    {
                        StructureId  = p.StructureId,
                        QueryType    = qt,
                        Points       = points,
                        Count        = points.Count,
                        NodesVisited = visited
                    });
                }

                default:
                    return ToolResult<SpatialOctreeQueryResult>.Fail(
                        $"Unknown QueryType '{p.QueryType}'. Use range | nearest | knn | radius.",
                        ErrorCodes.INVALID_PARAM);
            }
        }

        // ─────────────────────────────────────────────────────────────────
        // Range (AABB)
        // ─────────────────────────────────────────────────────────────────
        static void RangeSearch(OctreeNode node, Vector3 rMin, Vector3 rMax, int dims,
                                List<OctreePoint> output, ref int visited)
        {
            visited++;
            if (!OctreeStore.BoundsIntersectRange(node, rMin, rMax, dims)) return;

            if (node.IsLeaf)
            {
                foreach (var p in node.Points)
                    if (OctreeStore.PointInRange(p.Position, rMin, rMax, dims))
                        output.Add(p);
                return;
            }

            foreach (var child in node.Children)
                RangeSearch(child, rMin, rMax, dims, output, ref visited);
        }

        // ─────────────────────────────────────────────────────────────────
        // Radius (sphere)
        // ─────────────────────────────────────────────────────────────────
        static void RadiusSearch(OctreeNode node, Vector3 center, float rSq, int dims,
                                 List<(OctreePoint pt, float distSq)> output, ref int visited)
        {
            visited++;
            if (OctreeStore.BoundsMinDistSq(node, center, dims) > rSq) return;

            if (node.IsLeaf)
            {
                foreach (var p in node.Points)
                {
                    float d = OctreeStore.DistSq(p.Position, center, dims);
                    if (d <= rSq) output.Add((p, d));
                }
                return;
            }

            foreach (var child in node.Children)
                RadiusSearch(child, center, rSq, dims, output, ref visited);
        }

        // ─────────────────────────────────────────────────────────────────
        // K Nearest Neighbors (priority-queue descent using a sorted list)
        // ─────────────────────────────────────────────────────────────────
        static List<(OctreePoint pt, float distSq)> KnnSearch(OctreeNode root, Vector3 center,
                                                              int k, int dims, ref int visited)
        {
            // Max-heap of size k using a sorted list keyed by distSq (descending at tail).
            var best = new List<(OctreePoint pt, float distSq)>();

            // Min-priority list of nodes by min-bounds-distance to center
            var nodeHeap = new List<(OctreeNode node, float minDistSq)>
            {
                (root, OctreeStore.BoundsMinDistSq(root, center, dims))
            };

            while (nodeHeap.Count > 0)
            {
                // Pop node with smallest minDistSq (linear scan — fine for typical tree sizes)
                int bestIdx = 0;
                for (int i = 1; i < nodeHeap.Count; i++)
                    if (nodeHeap[i].minDistSq < nodeHeap[bestIdx].minDistSq)
                        bestIdx = i;

                var cur = nodeHeap[bestIdx];
                nodeHeap.RemoveAt(bestIdx);
                visited++;

                // If our best.Count == k, and this node's min distance is >=
                // the worst of best, we can stop.
                if (best.Count == k && cur.minDistSq > best[best.Count - 1].distSq)
                    break;

                if (cur.node.IsLeaf)
                {
                    foreach (var p in cur.node.Points)
                    {
                        float d = OctreeStore.DistSq(p.Position, center, dims);
                        if (best.Count < k)
                        {
                            InsertSorted(best, (p, d));
                        }
                        else if (d < best[best.Count - 1].distSq)
                        {
                            best.RemoveAt(best.Count - 1);
                            InsertSorted(best, (p, d));
                        }
                    }
                }
                else
                {
                    foreach (var child in cur.node.Children)
                    {
                        float mdSq = OctreeStore.BoundsMinDistSq(child, center, dims);
                        if (best.Count < k || mdSq <= best[best.Count - 1].distSq)
                            nodeHeap.Add((child, mdSq));
                    }
                }
            }

            return best;
        }

        static void InsertSorted(List<(OctreePoint pt, float distSq)> list,
                                 (OctreePoint pt, float distSq) item)
        {
            int i = 0;
            while (i < list.Count && list[i].distSq <= item.distSq) i++;
            list.Insert(i, item);
        }

        static SpatialOctreeQueryResult.QueryPoint ToQueryPoint(OctreePoint p, float distance)
            => new SpatialOctreeQueryResult.QueryPoint
            {
                Id       = p.Id,
                Position = new[] { p.Position.x, p.Position.y, p.Position.z },
                Data     = p.Data,
                Distance = distance
            };
    }
}

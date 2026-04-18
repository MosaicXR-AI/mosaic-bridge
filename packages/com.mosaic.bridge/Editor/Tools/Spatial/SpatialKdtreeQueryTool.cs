using System;
using System.Collections.Generic;
using System.Diagnostics;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Spatial
{
    public static class SpatialKdtreeQueryTool
    {
        [MosaicTool("spatial/kdtree-query",
                    "Queries a KD-tree built by spatial/kdtree-create. Supports nearest, knn, radius, and range queries",
                    isReadOnly: true, category: "spatial", Context = ToolContext.Both)]
        public static ToolResult<SpatialKdtreeQueryResult> Execute(SpatialKdtreeQueryParams p)
        {
            if (p == null)
                return ToolResult<SpatialKdtreeQueryResult>.Fail(
                    "Params are required", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrEmpty(p.StructureId))
                return ToolResult<SpatialKdtreeQueryResult>.Fail(
                    "StructureId is required", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrEmpty(p.QueryType))
                return ToolResult<SpatialKdtreeQueryResult>.Fail(
                    "QueryType is required", ErrorCodes.INVALID_PARAM);

            if (!SpatialKdtreeStore.Trees.TryGetValue(p.StructureId, out var tree))
                return ToolResult<SpatialKdtreeQueryResult>.Fail(
                    $"No KD-tree found with StructureId '{p.StructureId}'",
                    ErrorCodes.NOT_FOUND);

            int dim  = tree.Dimensions;
            string qt = p.QueryType.ToLowerInvariant();
            var sw    = Stopwatch.StartNew();
            int nodesVisited = 0;
            List<(SpatialKdtreeStore.StoredPoint pt, float dist)> hits;

            switch (qt)
            {
                case "nearest":
                {
                    if (p.Position == null || p.Position.Length != dim)
                        return ToolResult<SpatialKdtreeQueryResult>.Fail(
                            $"Position must have exactly {dim} components for nearest query",
                            ErrorCodes.INVALID_PARAM);

                    hits = Knn(tree.Root, p.Position, 1, dim, ref nodesVisited);
                    break;
                }
                case "knn":
                {
                    if (p.Position == null || p.Position.Length != dim)
                        return ToolResult<SpatialKdtreeQueryResult>.Fail(
                            $"Position must have exactly {dim} components for knn query",
                            ErrorCodes.INVALID_PARAM);
                    if (p.K <= 0)
                        return ToolResult<SpatialKdtreeQueryResult>.Fail(
                            "K must be > 0 for knn query", ErrorCodes.OUT_OF_RANGE);

                    hits = Knn(tree.Root, p.Position, p.K, dim, ref nodesVisited);
                    break;
                }
                case "radius":
                {
                    if (p.Position == null || p.Position.Length != dim)
                        return ToolResult<SpatialKdtreeQueryResult>.Fail(
                            $"Position must have exactly {dim} components for radius query",
                            ErrorCodes.INVALID_PARAM);
                    if (p.Radius < 0f)
                        return ToolResult<SpatialKdtreeQueryResult>.Fail(
                            "Radius must be >= 0", ErrorCodes.OUT_OF_RANGE);

                    hits = Radius(tree.Root, p.Position, p.Radius, dim, ref nodesVisited);
                    hits.Sort((a, b) => a.dist.CompareTo(b.dist));
                    break;
                }
                case "range":
                {
                    if (p.RangeMin == null || p.RangeMin.Length != dim ||
                        p.RangeMax == null || p.RangeMax.Length != dim)
                        return ToolResult<SpatialKdtreeQueryResult>.Fail(
                            $"RangeMin and RangeMax must each have {dim} components",
                            ErrorCodes.INVALID_PARAM);

                    hits = Range(tree.Root, p.RangeMin, p.RangeMax, dim, ref nodesVisited);
                    break;
                }
                default:
                    return ToolResult<SpatialKdtreeQueryResult>.Fail(
                        $"Unknown QueryType '{p.QueryType}'. Use nearest, knn, radius, or range.",
                        ErrorCodes.INVALID_PARAM);
            }
            sw.Stop();

            var resultPts = new SpatialKdtreeQueryResult.ResultPoint[hits.Count];
            for (int i = 0; i < hits.Count; i++)
            {
                var sp = hits[i].pt;
                var pos = new float[sp.Position.Length];
                Array.Copy(sp.Position, pos, sp.Position.Length);
                resultPts[i] = new SpatialKdtreeQueryResult.ResultPoint
                {
                    Id       = sp.Id,
                    Position = pos,
                    Data     = sp.Data,
                    Distance = hits[i].dist
                };
            }

            return ToolResult<SpatialKdtreeQueryResult>.Ok(new SpatialKdtreeQueryResult
            {
                StructureId  = p.StructureId,
                QueryType    = qt,
                Points       = resultPts,
                Count        = resultPts.Length,
                NodesVisited = nodesVisited,
                QueryTimeMs  = sw.ElapsedMilliseconds
            });
        }

        // ─── KNN (includes nearest as K=1) using bounded max-heap ───────
        static List<(SpatialKdtreeStore.StoredPoint, float)> Knn(
            SpatialKdtreeStore.KDNode root, float[] q, int k, int dim, ref int visited)
        {
            // Max-heap keyed by squared distance (largest on top).
            var heap = new List<(SpatialKdtreeStore.StoredPoint pt, float d2)>(k);
            int v = visited;
            KnnRecurse(root, q, k, dim, heap, ref v);
            visited = v;

            heap.Sort((a, b) => a.d2.CompareTo(b.d2));
            var outList = new List<(SpatialKdtreeStore.StoredPoint, float)>(heap.Count);
            foreach (var h in heap)
                outList.Add((h.pt, (float)Math.Sqrt(h.d2)));
            return outList;
        }

        static void KnnRecurse(
            SpatialKdtreeStore.KDNode node, float[] q, int k, int dim,
            List<(SpatialKdtreeStore.StoredPoint pt, float d2)> heap, ref int visited)
        {
            if (node == null) return;
            visited++;

            float d2 = SqDist(node.Point.Position, q, dim);

            if (heap.Count < k)
            {
                heap.Add((node.Point, d2));
                SiftUp(heap);
            }
            else if (d2 < heap[0].d2)
            {
                heap[0] = (node.Point, d2);
                SiftDown(heap);
            }

            int axis = node.Axis;
            float diff = q[axis] - node.Point.Position[axis];
            var near = diff < 0f ? node.Left : node.Right;
            var far  = diff < 0f ? node.Right : node.Left;

            KnnRecurse(near, q, k, dim, heap, ref visited);

            // Only descend into the far branch if it could contain a closer point.
            float worst = heap.Count < k ? float.PositiveInfinity : heap[0].d2;
            if (diff * diff < worst)
                KnnRecurse(far, q, k, dim, heap, ref visited);
        }

        // ─── Max-heap helpers (index 0 = largest d2) ────────────────────
        static void SiftUp(List<(SpatialKdtreeStore.StoredPoint pt, float d2)> h)
        {
            int i = h.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (h[i].d2 > h[parent].d2)
                {
                    (h[i], h[parent]) = (h[parent], h[i]);
                    i = parent;
                }
                else break;
            }
        }

        static void SiftDown(List<(SpatialKdtreeStore.StoredPoint pt, float d2)> h)
        {
            int i = 0, n = h.Count;
            while (true)
            {
                int l = 2 * i + 1, r = 2 * i + 2, largest = i;
                if (l < n && h[l].d2 > h[largest].d2) largest = l;
                if (r < n && h[r].d2 > h[largest].d2) largest = r;
                if (largest == i) return;
                (h[i], h[largest]) = (h[largest], h[i]);
                i = largest;
            }
        }

        // ─── Radius query ───────────────────────────────────────────────
        static List<(SpatialKdtreeStore.StoredPoint, float)> Radius(
            SpatialKdtreeStore.KDNode root, float[] q, float r, int dim, ref int visited)
        {
            var results = new List<(SpatialKdtreeStore.StoredPoint, float)>();
            float r2 = r * r;
            int v = visited;
            RadiusRecurse(root, q, r, r2, dim, results, ref v);
            visited = v;
            return results;
        }

        static void RadiusRecurse(
            SpatialKdtreeStore.KDNode node, float[] q, float r, float r2, int dim,
            List<(SpatialKdtreeStore.StoredPoint, float)> results, ref int visited)
        {
            if (node == null) return;
            visited++;

            float d2 = SqDist(node.Point.Position, q, dim);
            if (d2 <= r2)
                results.Add((node.Point, (float)Math.Sqrt(d2)));

            int axis = node.Axis;
            float diff = q[axis] - node.Point.Position[axis];

            if (diff <= 0f || diff * diff <= r2)
                RadiusRecurse(node.Left,  q, r, r2, dim, results, ref visited);
            if (diff >= 0f || diff * diff <= r2)
                RadiusRecurse(node.Right, q, r, r2, dim, results, ref visited);
        }

        // ─── Range query (axis-aligned box) ─────────────────────────────
        static List<(SpatialKdtreeStore.StoredPoint, float)> Range(
            SpatialKdtreeStore.KDNode root, float[] lo, float[] hi, int dim, ref int visited)
        {
            var results = new List<(SpatialKdtreeStore.StoredPoint, float)>();
            int v = visited;
            RangeRecurse(root, lo, hi, dim, results, ref v);
            visited = v;
            return results;
        }

        static void RangeRecurse(
            SpatialKdtreeStore.KDNode node, float[] lo, float[] hi, int dim,
            List<(SpatialKdtreeStore.StoredPoint, float)> results, ref int visited)
        {
            if (node == null) return;
            visited++;

            var pos = node.Point.Position;
            bool inside = true;
            for (int i = 0; i < dim; i++)
            {
                if (pos[i] < lo[i] || pos[i] > hi[i]) { inside = false; break; }
            }
            if (inside)
                results.Add((node.Point, 0f));

            int axis = node.Axis;
            // Left branch is relevant if the split plane >= lo[axis]
            if (node.Point.Position[axis] >= lo[axis])
                RangeRecurse(node.Left,  lo, hi, dim, results, ref visited);
            // Right branch is relevant if the split plane <= hi[axis]
            if (node.Point.Position[axis] <= hi[axis])
                RangeRecurse(node.Right, lo, hi, dim, results, ref visited);
        }

        // ─── Helpers ────────────────────────────────────────────────────
        static float SqDist(float[] a, float[] b, int dim)
        {
            float s = 0f;
            for (int i = 0; i < dim; i++)
            {
                float d = a[i] - b[i];
                s += d * d;
            }
            return s;
        }
    }
}

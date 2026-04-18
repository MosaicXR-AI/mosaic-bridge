using System;
using System.Collections.Generic;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.AI
{
    public static class AiPathfindJpsTool
    {
        [MosaicTool("ai/pathfind-jps",
                    "Performs Jump Point Search (JPS) pathfinding on a 2D grid — faster than A* by pruning symmetric paths",
                    isReadOnly: true, Context = ToolContext.Both)]
        public static ToolResult<AiPathfindJpsResult> Execute(AiPathfindJpsParams p)
        {
            // ── Validate grid ───────────────────────────────────────────
            if (p.GridWidth <= 0 || p.GridHeight <= 0)
                return ToolResult<AiPathfindJpsResult>.Fail(
                    "GridWidth and GridHeight must be positive", ErrorCodes.INVALID_PARAM);

            // ── Validate start / end arrays ─────────────────────────────
            if (p.Start == null || p.Start.Length != 2)
                return ToolResult<AiPathfindJpsResult>.Fail(
                    "Start must be an array of exactly 2 integers [x, y]", ErrorCodes.INVALID_PARAM);

            if (p.End == null || p.End.Length != 2)
                return ToolResult<AiPathfindJpsResult>.Fail(
                    "End must be an array of exactly 2 integers [x, y]", ErrorCodes.INVALID_PARAM);

            int sx = p.Start[0], sy = p.Start[1];
            int ex = p.End[0],   ey = p.End[1];

            if (sx < 0 || sx >= p.GridWidth || sy < 0 || sy >= p.GridHeight)
                return ToolResult<AiPathfindJpsResult>.Fail(
                    "Start position is outside the grid", ErrorCodes.OUT_OF_RANGE);

            if (ex < 0 || ex >= p.GridWidth || ey < 0 || ey >= p.GridHeight)
                return ToolResult<AiPathfindJpsResult>.Fail(
                    "End position is outside the grid", ErrorCodes.OUT_OF_RANGE);

            // ── Build obstacle set ──────────────────────────────────────
            var obstacles = new HashSet<long>();
            if (p.Obstacles != null)
            {
                foreach (var obs in p.Obstacles)
                {
                    if (obs == null || obs.Length != 2)
                        return ToolResult<AiPathfindJpsResult>.Fail(
                            "Each obstacle must be an array of exactly 2 integers [x, y]",
                            ErrorCodes.INVALID_PARAM);
                    obstacles.Add(Pack(obs[0], obs[1]));
                }
            }

            if (obstacles.Contains(Pack(sx, sy)))
                return ToolResult<AiPathfindJpsResult>.Fail(
                    "Start position is an obstacle", ErrorCodes.INVALID_PARAM);

            if (obstacles.Contains(Pack(ex, ey)))
                return ToolResult<AiPathfindJpsResult>.Fail(
                    "End position is an obstacle", ErrorCodes.INVALID_PARAM);

            // ── Trivial case: start == end ──────────────────────────────
            if (sx == ex && sy == ey)
            {
                return ToolResult<AiPathfindJpsResult>.Ok(new AiPathfindJpsResult
                {
                    Path          = Array.Empty<int[]>(),
                    PathLength    = 0f,
                    NodesExplored = 0,
                    Success       = true,
                    GridWidth     = p.GridWidth,
                    GridHeight    = p.GridHeight
                });
            }

            // ── Select heuristic ────────────────────────────────────────
            Func<int, int, int, int, float> heuristic;
            string hName = (p.Heuristic ?? "octile").ToLowerInvariant();
            switch (hName)
            {
                case "manhattan":
                    heuristic = (x1, y1, x2, y2) =>
                    {
                        return Mathf.Abs(x2 - x1) + Mathf.Abs(y2 - y1);
                    };
                    break;
                case "euclidean":
                    heuristic = (x1, y1, x2, y2) =>
                    {
                        float dx = x2 - x1;
                        float dy = y2 - y1;
                        return Mathf.Sqrt(dx * dx + dy * dy);
                    };
                    break;
                case "octile":
                    heuristic = (x1, y1, x2, y2) =>
                    {
                        float dx = Mathf.Abs(x2 - x1);
                        float dy = Mathf.Abs(y2 - y1);
                        return Mathf.Max(dx, dy) + (1.41421356f - 1f) * Mathf.Min(dx, dy);
                    };
                    break;
                default:
                    return ToolResult<AiPathfindJpsResult>.Fail(
                        $"Unknown heuristic '{p.Heuristic}'. Use manhattan, euclidean, or octile.",
                        ErrorCodes.INVALID_PARAM);
            }

            // ── JPS with A* open list ───────────────────────────────────
            bool allowDiag = p.DiagonalMovement;
            int w = p.GridWidth, h = p.GridHeight;

            var openSet = new SortedSet<Node>(Comparer<Node>.Create((a, b) =>
            {
                int cmp = a.f.CompareTo(b.f);
                if (cmp != 0) return cmp;
                cmp = a.h.CompareTo(b.h);
                if (cmp != 0) return cmp;
                cmp = a.x.CompareTo(b.x);
                if (cmp != 0) return cmp;
                return a.y.CompareTo(b.y);
            }));

            var gCost    = new Dictionary<long, float>();
            var cameFrom = new Dictionary<long, long>();
            var closed   = new HashSet<long>();
            int explored = 0;

            long startKey = Pack(sx, sy);
            long endKey   = Pack(ex, ey);

            gCost[startKey] = 0f;
            float startH = heuristic(sx, sy, ex, ey);
            openSet.Add(new Node(sx, sy, 0f, startH));

            bool found = false;

            while (openSet.Count > 0)
            {
                var cur = openSet.Min;
                openSet.Remove(cur);

                long curKey = Pack(cur.x, cur.y);
                if (closed.Contains(curKey)) continue;
                closed.Add(curKey);
                explored++;

                if (curKey == endKey) { found = true; break; }

                // Identify successors via jumping
                var successors = IdentifySuccessors(
                    cur.x, cur.y, curKey, cameFrom, obstacles, w, h, ex, ey, allowDiag);

                foreach (var jp in successors)
                {
                    long jpKey = Pack(jp.x, jp.y);
                    if (closed.Contains(jpKey)) continue;

                    float dx = Mathf.Abs(jp.x - cur.x);
                    float dy = Mathf.Abs(jp.y - cur.y);
                    float dist = (dx != 0 && dy != 0)
                        ? Mathf.Max(dx, dy) * 1.41421356f   // diagonal segment
                        : dx + dy;                           // cardinal segment

                    // For mixed diagonal+cardinal jumps compute exact cost
                    if (dx != 0 && dy != 0)
                    {
                        float diag = Mathf.Min(dx, dy);
                        float card = Mathf.Abs(dx - dy);
                        dist = diag * 1.41421356f + card;
                    }

                    float tentG = gCost[curKey] + dist;

                    if (!gCost.ContainsKey(jpKey) || tentG < gCost[jpKey])
                    {
                        gCost[jpKey]    = tentG;
                        cameFrom[jpKey] = curKey;
                        float hVal = heuristic(jp.x, jp.y, ex, ey);
                        openSet.Add(new Node(jp.x, jp.y, tentG, hVal));
                    }
                }
            }

            if (!found)
            {
                return ToolResult<AiPathfindJpsResult>.Ok(new AiPathfindJpsResult
                {
                    Path          = Array.Empty<int[]>(),
                    PathLength    = 0f,
                    NodesExplored = explored,
                    Success       = false,
                    GridWidth     = w,
                    GridHeight    = h
                });
            }

            // ── Reconstruct jump-point path ─────────────────────────────
            var jpPath = new List<int[]>();
            long trace = endKey;
            while (trace != startKey)
            {
                int tx, ty;
                Unpack(trace, out tx, out ty);
                jpPath.Add(new[] { tx, ty });
                trace = cameFrom[trace];
            }
            jpPath.Add(new[] { sx, sy });
            jpPath.Reverse();

            // Compute full path length by summing segment costs
            float totalLength = 0f;
            for (int i = 1; i < jpPath.Count; i++)
            {
                float ddx = Mathf.Abs(jpPath[i][0] - jpPath[i - 1][0]);
                float ddy = Mathf.Abs(jpPath[i][1] - jpPath[i - 1][1]);
                float diag = Mathf.Min(ddx, ddy);
                float card = Mathf.Abs(ddx - ddy);
                totalLength += diag * 1.41421356f + card;
            }

            return ToolResult<AiPathfindJpsResult>.Ok(new AiPathfindJpsResult
            {
                Path          = jpPath.ToArray(),
                PathLength    = totalLength,
                NodesExplored = explored,
                Success       = true,
                GridWidth     = w,
                GridHeight    = h
            });
        }

        // ─────────────────────────────────────────────────────────────────
        // JPS: identify successors from a node
        // ─────────────────────────────────────────────────────────────────
        static List<(int x, int y)> IdentifySuccessors(
            int cx, int cy, long curKey,
            Dictionary<long, long> cameFrom,
            HashSet<long> obstacles,
            int w, int h,
            int ex, int ey,
            bool allowDiag)
        {
            var result = new List<(int x, int y)>();
            var neighbors = PrunedNeighbors(cx, cy, curKey, cameFrom, obstacles, w, h, allowDiag);

            foreach (var (nx, ny) in neighbors)
            {
                int dx = nx - cx;
                int dy = ny - cy;
                // Normalize direction
                int ddx = dx != 0 ? (dx > 0 ? 1 : -1) : 0;
                int ddy = dy != 0 ? (dy > 0 ? 1 : -1) : 0;

                var jp = Jump(cx, cy, ddx, ddy, obstacles, w, h, ex, ey, allowDiag);
                if (jp.HasValue)
                    result.Add(jp.Value);
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────
        // JPS: pruned neighbors (natural + forced)
        // ─────────────────────────────────────────────────────────────────
        static List<(int x, int y)> PrunedNeighbors(
            int cx, int cy, long curKey,
            Dictionary<long, long> cameFrom,
            HashSet<long> obstacles,
            int w, int h,
            bool allowDiag)
        {
            var result = new List<(int, int)>();

            // If this is the start node (no parent), return all walkable neighbors
            if (!cameFrom.ContainsKey(curKey))
            {
                // Cardinal
                if (Walkable(cx,     cy - 1, obstacles, w, h)) result.Add((cx,     cy - 1));
                if (Walkable(cx + 1, cy,     obstacles, w, h)) result.Add((cx + 1, cy));
                if (Walkable(cx,     cy + 1, obstacles, w, h)) result.Add((cx,     cy + 1));
                if (Walkable(cx - 1, cy,     obstacles, w, h)) result.Add((cx - 1, cy));
                if (allowDiag)
                {
                    if (Walkable(cx + 1, cy + 1, obstacles, w, h)) result.Add((cx + 1, cy + 1));
                    if (Walkable(cx + 1, cy - 1, obstacles, w, h)) result.Add((cx + 1, cy - 1));
                    if (Walkable(cx - 1, cy + 1, obstacles, w, h)) result.Add((cx - 1, cy + 1));
                    if (Walkable(cx - 1, cy - 1, obstacles, w, h)) result.Add((cx - 1, cy - 1));
                }
                return result;
            }

            // Determine parent direction
            int px, py;
            Unpack(cameFrom[curKey], out px, out py);
            int dx = Clamp1(cx - px);
            int dy = Clamp1(cy - py);

            if (allowDiag)
            {
                if (dx != 0 && dy != 0)
                {
                    // Diagonal movement: natural neighbors
                    if (Walkable(cx, cy + dy, obstacles, w, h))
                        result.Add((cx, cy + dy));
                    if (Walkable(cx + dx, cy, obstacles, w, h))
                        result.Add((cx + dx, cy));
                    if (Walkable(cx + dx, cy + dy, obstacles, w, h))
                        result.Add((cx + dx, cy + dy));

                    // Forced neighbors
                    if (!Walkable(cx - dx, cy, obstacles, w, h) &&
                        Walkable(cx - dx, cy + dy, obstacles, w, h))
                        result.Add((cx - dx, cy + dy));

                    if (!Walkable(cx, cy - dy, obstacles, w, h) &&
                        Walkable(cx + dx, cy - dy, obstacles, w, h))
                        result.Add((cx + dx, cy - dy));
                }
                else if (dx != 0)
                {
                    // Horizontal movement
                    if (Walkable(cx + dx, cy, obstacles, w, h))
                        result.Add((cx + dx, cy));

                    // Forced neighbors
                    if (!Walkable(cx, cy + 1, obstacles, w, h) &&
                        Walkable(cx + dx, cy + 1, obstacles, w, h))
                        result.Add((cx + dx, cy + 1));

                    if (!Walkable(cx, cy - 1, obstacles, w, h) &&
                        Walkable(cx + dx, cy - 1, obstacles, w, h))
                        result.Add((cx + dx, cy - 1));
                }
                else // dy != 0
                {
                    // Vertical movement
                    if (Walkable(cx, cy + dy, obstacles, w, h))
                        result.Add((cx, cy + dy));

                    // Forced neighbors
                    if (!Walkable(cx + 1, cy, obstacles, w, h) &&
                        Walkable(cx + 1, cy + dy, obstacles, w, h))
                        result.Add((cx + 1, cy + dy));

                    if (!Walkable(cx - 1, cy, obstacles, w, h) &&
                        Walkable(cx - 1, cy + dy, obstacles, w, h))
                        result.Add((cx - 1, cy + dy));
                }
            }
            else
            {
                // Cardinal only
                if (dx != 0)
                {
                    if (Walkable(cx + dx, cy, obstacles, w, h))
                        result.Add((cx + dx, cy));
                    if (!Walkable(cx, cy + 1, obstacles, w, h))
                    {
                        if (Walkable(cx + dx, cy + 1, obstacles, w, h))
                            result.Add((cx + dx, cy + 1));
                    }
                    if (!Walkable(cx, cy - 1, obstacles, w, h))
                    {
                        if (Walkable(cx + dx, cy - 1, obstacles, w, h))
                            result.Add((cx + dx, cy - 1));
                    }
                }
                else if (dy != 0)
                {
                    if (Walkable(cx, cy + dy, obstacles, w, h))
                        result.Add((cx, cy + dy));
                    if (!Walkable(cx + 1, cy, obstacles, w, h))
                    {
                        if (Walkable(cx + 1, cy + dy, obstacles, w, h))
                            result.Add((cx + 1, cy + dy));
                    }
                    if (!Walkable(cx - 1, cy, obstacles, w, h))
                    {
                        if (Walkable(cx - 1, cy + dy, obstacles, w, h))
                            result.Add((cx - 1, cy + dy));
                    }
                }
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────
        // JPS: Jump function (Harabor & Grastien 2011)
        // ─────────────────────────────────────────────────────────────────
        static (int x, int y)? Jump(
            int cx, int cy, int dx, int dy,
            HashSet<long> obstacles,
            int w, int h,
            int ex, int ey,
            bool allowDiag)
        {
            int nx = cx + dx;
            int ny = cy + dy;

            if (!Walkable(nx, ny, obstacles, w, h))
                return null;

            if (nx == ex && ny == ey)
                return (nx, ny);

            if (allowDiag)
            {
                // Check for forced neighbors
                if (dx != 0 && dy != 0)
                {
                    // Diagonal: forced neighbor check
                    if ((!Walkable(nx - dx, ny, obstacles, w, h) &&
                         Walkable(nx - dx, ny + dy, obstacles, w, h)) ||
                        (!Walkable(nx, ny - dy, obstacles, w, h) &&
                         Walkable(nx + dx, ny - dy, obstacles, w, h)))
                        return (nx, ny);

                    // When moving diagonally, also jump cardinally
                    if (Jump(nx, ny, dx, 0, obstacles, w, h, ex, ey, allowDiag).HasValue ||
                        Jump(nx, ny, 0, dy, obstacles, w, h, ex, ey, allowDiag).HasValue)
                        return (nx, ny);
                }
                else
                {
                    // Cardinal: forced neighbor check
                    if (dx != 0)
                    {
                        if ((!Walkable(nx, ny + 1, obstacles, w, h) &&
                             Walkable(nx + dx, ny + 1, obstacles, w, h)) ||
                            (!Walkable(nx, ny - 1, obstacles, w, h) &&
                             Walkable(nx + dx, ny - 1, obstacles, w, h)))
                            return (nx, ny);
                    }
                    else
                    {
                        if ((!Walkable(nx + 1, ny, obstacles, w, h) &&
                             Walkable(nx + 1, ny + dy, obstacles, w, h)) ||
                            (!Walkable(nx - 1, ny, obstacles, w, h) &&
                             Walkable(nx - 1, ny + dy, obstacles, w, h)))
                            return (nx, ny);
                    }
                }
            }
            else
            {
                // Cardinal only mode
                if (dx != 0)
                {
                    if ((!Walkable(nx, ny + 1, obstacles, w, h) &&
                         Walkable(nx + dx, ny + 1, obstacles, w, h)) ||
                        (!Walkable(nx, ny - 1, obstacles, w, h) &&
                         Walkable(nx + dx, ny - 1, obstacles, w, h)))
                        return (nx, ny);
                }
                else if (dy != 0)
                {
                    if ((!Walkable(nx + 1, ny, obstacles, w, h) &&
                         Walkable(nx + 1, ny + dy, obstacles, w, h)) ||
                        (!Walkable(nx - 1, ny, obstacles, w, h) &&
                         Walkable(nx - 1, ny + dy, obstacles, w, h)))
                        return (nx, ny);
                }
            }

            return Jump(nx, ny, dx, dy, obstacles, w, h, ex, ey, allowDiag);
        }

        // ─────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────
        static bool Walkable(int x, int y, HashSet<long> obstacles, int w, int h)
        {
            if (x < 0 || x >= w || y < 0 || y >= h) return false;
            return !obstacles.Contains(Pack(x, y));
        }

        static long Pack(int x, int y) => ((long)x << 32) | (uint)y;

        static void Unpack(long key, out int x, out int y)
        {
            x = (int)(key >> 32);
            y = (int)(key & 0xFFFFFFFF);
        }

        static int Clamp1(int v) => v > 0 ? 1 : (v < 0 ? -1 : 0);

        struct Node
        {
            public int   x, y;
            public float g, h, f;

            public Node(int x, int y, float g, float h)
            {
                this.x = x;
                this.y = y;
                this.g = g;
                this.h = h;
                this.f = g + h;
            }
        }
    }
}

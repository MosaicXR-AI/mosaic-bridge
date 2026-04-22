using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.ProcGen
{
    public static class ProcGenWfcTool
    {
        // Direction vectors: right, left, up, down, forward, back
        static readonly string[] DirectionNames = { "right", "left", "up", "down", "forward", "back" };
        static readonly int[][] DirectionOffsets =
        {
            new[] {  1,  0,  0 }, // right  (+x)
            new[] { -1,  0,  0 }, // left   (-x)
            new[] {  0,  1,  0 }, // up     (+y)
            new[] {  0, -1,  0 }, // down   (-y)
            new[] {  0,  0,  1 }, // forward(+z)
            new[] {  0,  0, -1 }, // back   (-z)
        };

        // Opposite direction indices
        static readonly int[] Opposite = { 1, 0, 3, 2, 5, 4 };

        [MosaicTool("procgen/wfc",
                    "Runs Wave Function Collapse to fill a grid with tiles satisfying adjacency constraints. " +
                    "Tiles param is required: [{\"Id\":\"floor\",\"Weight\":1.0,\"AllowedNeighbors\":{\"right\":[\"floor\",\"wall\"],\"left\":[\"floor\"],\"up\":[\"floor\"],\"down\":[\"floor\"]}}]. " +
                    "Direction keys: right/left/up/down (2D); also forward/back for 3D (Depth>1). " +
                    "AllowedNeighbors maps each direction to a list of tile Ids allowed as neighbors.",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<ProcGenWfcResult> Execute(ProcGenWfcParams p)
        {
            // --- Validate --------------------------------------------------------
            if (p.Width < 1 || p.Height < 1 || p.Depth < 1)
                return ToolResult<ProcGenWfcResult>.Fail(
                    "Width, Height and Depth must be >= 1", ErrorCodes.INVALID_PARAM);

            if (p.Tiles == null || p.Tiles.Count == 0)
                return ToolResult<ProcGenWfcResult>.Fail(
                    "At least one tile must be provided", ErrorCodes.INVALID_PARAM);

            // Build tile index lookup
            var tileIds = new List<string>();
            var tileIndex = new Dictionary<string, int>();
            var tileWeights = new List<float>();
            for (int i = 0; i < p.Tiles.Count; i++)
            {
                var t = p.Tiles[i];
                if (string.IsNullOrEmpty(t.Id))
                    return ToolResult<ProcGenWfcResult>.Fail(
                        $"Tile at index {i} has no Id", ErrorCodes.INVALID_PARAM);
                if (tileIndex.ContainsKey(t.Id))
                    return ToolResult<ProcGenWfcResult>.Fail(
                        $"Duplicate tile Id '{t.Id}'", ErrorCodes.INVALID_PARAM);
                tileIndex[t.Id] = i;
                tileIds.Add(t.Id);
                tileWeights.Add(t.Weight > 0 ? t.Weight : 1f);
            }

            int tileCount = tileIds.Count;
            int dirCount  = p.Depth > 1 ? 6 : 4; // 2D uses right/left/up/down only

            // --- Build adjacency bitmask tables ----------------------------------
            // allowed[dir][tileA] is a bitset of tiles allowed as neighbor of tileA in direction dir
            var allowed = new ulong[dirCount][];
            int ulongCount = (tileCount + 63) / 64;

            for (int d = 0; d < dirCount; d++)
            {
                allowed[d] = new ulong[tileCount * ulongCount];
                // default: nothing allowed (must be explicitly listed)
            }

            for (int ti = 0; ti < tileCount; ti++)
            {
                var tileDef = p.Tiles[ti];
                if (tileDef.AllowedNeighbors == null) continue;

                for (int d = 0; d < dirCount; d++)
                {
                    string dirName = DirectionNames[d];
                    if (!tileDef.AllowedNeighbors.TryGetValue(dirName, out var neighborIds))
                        continue;
                    foreach (var nid in neighborIds)
                    {
                        if (!tileIndex.TryGetValue(nid, out int ni)) continue;
                        allowed[d][ti * ulongCount + ni / 64] |= 1UL << (ni % 64);
                    }
                }
            }

            // --- Prepare grid state ----------------------------------------------
            int w = p.Width, h = p.Height, depth = p.Depth;
            int totalCells = w * h * depth;

            // possibilities[cell] = bitset of remaining tile options
            var possibilities = new ulong[totalCells * ulongCount];
            var countRemaining = new int[totalCells];

            ulong[] fullSet = new ulong[ulongCount];
            for (int t = 0; t < tileCount; t++)
                fullSet[t / 64] |= 1UL << (t % 64);

            for (int c = 0; c < totalCells; c++)
            {
                for (int u = 0; u < ulongCount; u++)
                    possibilities[c * ulongCount + u] = fullSet[u];
                countRemaining[c] = tileCount;
            }

            // --- RNG -------------------------------------------------------------
            int seed = p.Seed ?? Environment.TickCount;
            var rng = new System.Random(seed);

            // --- Helpers ---------------------------------------------------------
            int CellIndex(int x, int y, int z) => x + y * w + z * w * h;

            void GetCoords(int idx, out int x, out int y, out int z)
            {
                z = idx / (w * h);
                int rem = idx % (w * h);
                y = rem / w;
                x = rem % w;
            }

            bool HasBit(ulong[] set, int offset, int bit) =>
                (set[offset + bit / 64] & (1UL << (bit % 64))) != 0;

            void ClearBit(ulong[] set, int offset, int bit) =>
                set[offset + bit / 64] &= ~(1UL << (bit % 64));

            int CountBits(ulong[] set, int offset)
            {
                int c = 0;
                for (int u = 0; u < ulongCount; u++)
                    c += BitCount(set[offset + u]);
                return c;
            }

            // --- Backtrack stack -------------------------------------------------
            int backtracks = 0;
            int backtrackLimit = p.BacktrackLimit;
            var historyStack = new Stack<(int cell, int chosenTile, ulong[] savedPossibilities, int[] savedCounts)>();

            // --- Propagation queue -----------------------------------------------
            bool Propagate(int startCell)
            {
                var queue = new Queue<int>();
                queue.Enqueue(startCell);

                while (queue.Count > 0)
                {
                    int cell = queue.Dequeue();
                    GetCoords(cell, out int cx, out int cy, out int cz);

                    for (int d = 0; d < dirCount; d++)
                    {
                        int nx = cx + DirectionOffsets[d][0];
                        int ny = cy + DirectionOffsets[d][1];
                        int nz = cz + DirectionOffsets[d][2];

                        if (nx < 0 || nx >= w || ny < 0 || ny >= h || nz < 0 || nz >= depth)
                            continue;

                        int neighbor = CellIndex(nx, ny, nz);
                        if (countRemaining[neighbor] <= 1) continue;

                        // Compute union of what current cell allows in this direction
                        ulong[] unionAllowed = new ulong[ulongCount];
                        int cellOff = cell * ulongCount;
                        for (int t = 0; t < tileCount; t++)
                        {
                            if (!HasBit(possibilities, cellOff, t)) continue;
                            int allowOff = t * ulongCount;
                            for (int u = 0; u < ulongCount; u++)
                                unionAllowed[u] |= allowed[d][allowOff + u];
                        }

                        // Intersect with neighbor possibilities
                        int nOff = neighbor * ulongCount;
                        bool changed = false;
                        for (int u = 0; u < ulongCount; u++)
                        {
                            ulong before = possibilities[nOff + u];
                            ulong after = before & unionAllowed[u];
                            if (after != before)
                            {
                                possibilities[nOff + u] = after;
                                changed = true;
                            }
                        }

                        if (changed)
                        {
                            int newCount = CountBits(possibilities, nOff);
                            countRemaining[neighbor] = newCount;
                            if (newCount == 0) return false; // contradiction
                            queue.Enqueue(neighbor);
                        }
                    }
                }
                return true;
            }

            // --- Main WFC loop ---------------------------------------------------
            bool success = true;
            while (true)
            {
                // Find uncollapsed cell with minimum entropy
                int minCount = int.MaxValue;
                int bestCell = -1;

                for (int c = 0; c < totalCells; c++)
                {
                    int rem = countRemaining[c];
                    if (rem <= 1) continue;
                    // Jitter for tie-breaking
                    double jittered = rem + rng.NextDouble() * 0.1;
                    if (jittered < minCount)
                    {
                        minCount = rem;
                        bestCell = c;
                    }
                }

                if (bestCell == -1) break; // all collapsed

                // Save state for backtracking
                ulong[] savedPoss = new ulong[possibilities.Length];
                Array.Copy(possibilities, savedPoss, possibilities.Length);
                int[] savedCounts = new int[countRemaining.Length];
                Array.Copy(countRemaining, savedCounts, countRemaining.Length);

                // Weighted random collapse
                int chosenTile = WeightedCollapse(possibilities, bestCell * ulongCount,
                    tileWeights, tileCount, ulongCount, rng);

                // Collapse cell to chosen tile
                int bOff = bestCell * ulongCount;
                for (int u = 0; u < ulongCount; u++)
                    possibilities[bOff + u] = 0;
                possibilities[bOff + chosenTile / 64] = 1UL << (chosenTile % 64);
                countRemaining[bestCell] = 1;

                historyStack.Push((bestCell, chosenTile, savedPoss, savedCounts));

                if (!Propagate(bestCell))
                {
                    // Contradiction — backtrack
                    bool recovered = false;
                    while (historyStack.Count > 0 && backtracks < backtrackLimit)
                    {
                        backtracks++;
                        var (bCell, bTile, bPoss, bCounts) = historyStack.Pop();

                        // Restore state
                        Array.Copy(bPoss, possibilities, possibilities.Length);
                        Array.Copy(bCounts, countRemaining, countRemaining.Length);

                        // Remove the tile that caused contradiction
                        int off = bCell * ulongCount;
                        ClearBit(possibilities, off, bTile);
                        countRemaining[bCell] = CountBits(possibilities, off);

                        if (countRemaining[bCell] == 0) continue; // keep backtracking

                        if (Propagate(bCell))
                        {
                            recovered = true;
                            break;
                        }
                    }

                    if (!recovered)
                    {
                        success = false;
                        break;
                    }
                }
            }

            // --- Build result grid -----------------------------------------------
            int collapsedCells = 0;
            var grid = new string[totalCells][];
            for (int c = 0; c < totalCells; c++)
            {
                var cellTiles = new List<string>();
                int off = c * ulongCount;
                for (int t = 0; t < tileCount; t++)
                {
                    if (HasBit(possibilities, off, t))
                        cellTiles.Add(tileIds[t]);
                }
                grid[c] = cellTiles.ToArray();
                if (cellTiles.Count == 1) collapsedCells++;
            }

            // --- Optional prefab instantiation -----------------------------------
            string[] goNames = null;
            if (success && p.PrefabMapping != null && p.PrefabMapping.Count > 0)
            {
                var prefabLookup = new Dictionary<string, string>();
                foreach (var pm in p.PrefabMapping)
                {
                    if (!string.IsNullOrEmpty(pm.TileId) && !string.IsNullOrEmpty(pm.PrefabPath))
                        prefabLookup[pm.TileId] = pm.PrefabPath;
                }

                GameObject parent = null;
                if (!string.IsNullOrEmpty(p.ParentObject))
                {
                    parent = GameObject.Find(p.ParentObject);
                    if (parent == null)
                    {
                        parent = new GameObject(p.ParentObject);
                        Undo.RegisterCreatedObjectUndo(parent, "WFC Parent");
                    }
                }

                var names = new List<string>();
                float cs = p.CellSize;
                for (int c = 0; c < totalCells; c++)
                {
                    if (grid[c].Length != 1) continue;
                    string tid = grid[c][0];
                    if (!prefabLookup.TryGetValue(tid, out string prefabPath)) continue;

                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    if (prefab == null) continue;

                    GetCoords(c, out int gx, out int gy, out int gz);
                    var pos = new Vector3(gx * cs, gy * cs, gz * cs);

                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    go.transform.position = pos;
                    go.name = $"WFC_{tid}_{gx}_{gy}_{gz}";
                    if (parent != null) go.transform.SetParent(parent.transform, true);
                    Undo.RegisterCreatedObjectUndo(go, "WFC Tile");
                    names.Add(go.name);
                }
                goNames = names.ToArray();
            }

            return ToolResult<ProcGenWfcResult>.Ok(new ProcGenWfcResult
            {
                Width          = w,
                Height         = h,
                Depth          = depth,
                Grid           = grid,
                Success        = success,
                Backtracks     = backtracks,
                GameObjectNames = goNames,
                CollapsedCells = collapsedCells,
                TotalCells     = totalCells
            });
        }

        static int WeightedCollapse(ulong[] possibilities, int offset,
            List<float> weights, int tileCount, int ulongCount, System.Random rng)
        {
            float totalWeight = 0;
            for (int t = 0; t < tileCount; t++)
            {
                if ((possibilities[offset + t / 64] & (1UL << (t % 64))) != 0)
                    totalWeight += weights[t];
            }

            float roll = (float)(rng.NextDouble() * totalWeight);
            float acc = 0;
            for (int t = 0; t < tileCount; t++)
            {
                if ((possibilities[offset + t / 64] & (1UL << (t % 64))) == 0) continue;
                acc += weights[t];
                if (acc >= roll) return t;
            }

            // Fallback: return last available
            for (int t = tileCount - 1; t >= 0; t--)
            {
                if ((possibilities[offset + t / 64] & (1UL << (t % 64))) != 0) return t;
            }
            return 0;
        }

        static int BitCount(ulong v)
        {
            // Hamming weight
            v = v - ((v >> 1) & 0x5555555555555555UL);
            v = (v & 0x3333333333333333UL) + ((v >> 2) & 0x3333333333333333UL);
            return (int)(((v + (v >> 4)) & 0x0F0F0F0F0F0F0F0FUL) * 0x0101010101010101UL >> 56);
        }
    }
}

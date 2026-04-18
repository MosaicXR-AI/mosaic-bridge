using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.AI
{
    public static class AiFlowFieldTool
    {
        [MosaicTool("ai/flowfield-generate",
                    "Generates a flow field for grid-based pathfinding toward one or more target cells",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<AiFlowFieldResult> Execute(AiFlowFieldParams p)
        {
            // ---- Validate inputs ----
            if (p.GridWidth <= 0 || p.GridHeight <= 0)
                return ToolResult<AiFlowFieldResult>.Fail(
                    "GridWidth and GridHeight must be positive", ErrorCodes.INVALID_PARAM);

            if (p.Targets == null || p.Targets.Length == 0)
                return ToolResult<AiFlowFieldResult>.Fail(
                    "At least one target cell is required", ErrorCodes.INVALID_PARAM);

            int totalCells = p.GridWidth * p.GridHeight;

            // ---- Build cost field ----
            float[] costField = new float[totalCells];
            if (p.CostField != null)
            {
                if (p.CostField.Length != totalCells)
                    return ToolResult<AiFlowFieldResult>.Fail(
                        $"CostField length ({p.CostField.Length}) must equal GridWidth*GridHeight ({totalCells})",
                        ErrorCodes.INVALID_PARAM);
                Array.Copy(p.CostField, costField, totalCells);
            }
            else
            {
                for (int i = 0; i < totalCells; i++)
                    costField[i] = 1.0f;
            }

            // ---- Mark obstacles ----
            var obstacleSet = new HashSet<int>();
            if (p.Obstacles != null)
            {
                foreach (var obs in p.Obstacles)
                {
                    if (obs == null || obs.Length != 2)
                        return ToolResult<AiFlowFieldResult>.Fail(
                            "Each obstacle must be an [x,y] pair", ErrorCodes.INVALID_PARAM);
                    int ox = obs[0], oy = obs[1];
                    if (ox < 0 || ox >= p.GridWidth || oy < 0 || oy >= p.GridHeight)
                        continue; // silently ignore out-of-bounds obstacles
                    int idx = oy * p.GridWidth + ox;
                    costField[idx] = float.MaxValue;
                    obstacleSet.Add(idx);
                }
            }

            // ---- Validate targets ----
            var targetIndices = new List<int>();
            foreach (var t in p.Targets)
            {
                if (t == null || t.Length != 2)
                    return ToolResult<AiFlowFieldResult>.Fail(
                        "Each target must be an [x,y] pair", ErrorCodes.INVALID_PARAM);
                int tx = t[0], ty = t[1];
                if (tx < 0 || tx >= p.GridWidth || ty < 0 || ty >= p.GridHeight)
                    return ToolResult<AiFlowFieldResult>.Fail(
                        $"Target [{tx},{ty}] is outside the grid", ErrorCodes.OUT_OF_RANGE);
                int idx = ty * p.GridWidth + tx;
                if (obstacleSet.Contains(idx))
                    return ToolResult<AiFlowFieldResult>.Fail(
                        $"Target [{tx},{ty}] is an obstacle cell", ErrorCodes.INVALID_PARAM);
                targetIndices.Add(idx);
            }

            // ---- Integration field (Dijkstra from targets) ----
            float[] integrationField = new float[totalCells];
            for (int i = 0; i < totalCells; i++)
                integrationField[i] = float.MaxValue;

            // Priority queue: (distance, cellIndex)
            var openList = new SortedSet<(float dist, int idx)>(
                Comparer<(float dist, int idx)>.Create((a, b) =>
                {
                    int cmp = a.dist.CompareTo(b.dist);
                    return cmp != 0 ? cmp : a.idx.CompareTo(b.idx);
                }));

            foreach (int tIdx in targetIndices)
            {
                integrationField[tIdx] = 0f;
                openList.Add((0f, tIdx));
            }

            // 8-directional neighbors
            int[] dx = { 0, 1, 1, 1, 0, -1, -1, -1 };
            int[] dy = { -1, -1, 0, 1, 1, 1, 0, -1 };
            float[] moveCosts = { 1f, 1.414f, 1f, 1.414f, 1f, 1.414f, 1f, 1.414f };

            while (openList.Count > 0)
            {
                var current = openList.Min;
                openList.Remove(current);

                int cx = current.idx % p.GridWidth;
                int cy = current.idx / p.GridWidth;

                if (current.dist > integrationField[current.idx])
                    continue; // stale entry

                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + dx[d];
                    int ny = cy + dy[d];

                    if (nx < 0 || nx >= p.GridWidth || ny < 0 || ny >= p.GridHeight)
                        continue;

                    int nIdx = ny * p.GridWidth + nx;

                    if (costField[nIdx] >= float.MaxValue)
                        continue; // obstacle

                    // For diagonal movement, check that both cardinal neighbors are passable
                    if (dx[d] != 0 && dy[d] != 0)
                    {
                        int cardinalIdx1 = cy * p.GridWidth + (cx + dx[d]);
                        int cardinalIdx2 = (cy + dy[d]) * p.GridWidth + cx;
                        if (costField[cardinalIdx1] >= float.MaxValue ||
                            costField[cardinalIdx2] >= float.MaxValue)
                            continue; // can't cut corners
                    }

                    float newCost = integrationField[current.idx] + moveCosts[d] * costField[nIdx];

                    if (newCost < integrationField[nIdx])
                    {
                        integrationField[nIdx] = newCost;
                        openList.Add((newCost, nIdx));
                    }
                }
            }

            // ---- Flow field (gradient descent) ----
            float[][] flowField = new float[totalCells][];
            int reachable = 0;
            int unreachable = 0;

            for (int i = 0; i < totalCells; i++)
            {
                if (costField[i] >= float.MaxValue)
                {
                    // Obstacle: zero vector
                    flowField[i] = new[] { 0f, 0f };
                    unreachable++;
                    continue;
                }

                if (integrationField[i] >= float.MaxValue)
                {
                    // Unreachable cell
                    flowField[i] = new[] { 0f, 0f };
                    unreachable++;
                    continue;
                }

                // Target cells get zero vector
                if (integrationField[i] == 0f)
                {
                    flowField[i] = new[] { 0f, 0f };
                    reachable++;
                    continue;
                }

                int cx = i % p.GridWidth;
                int cy = i / p.GridWidth;

                float bestDist = integrationField[i];
                float bestDx = 0f, bestDy = 0f;

                for (int d = 0; d < 8; d++)
                {
                    int nx = cx + dx[d];
                    int ny = cy + dy[d];

                    if (nx < 0 || nx >= p.GridWidth || ny < 0 || ny >= p.GridHeight)
                        continue;

                    int nIdx = ny * p.GridWidth + nx;
                    if (integrationField[nIdx] < bestDist)
                    {
                        bestDist = integrationField[nIdx];
                        bestDx = dx[d];
                        bestDy = dy[d];
                    }
                }

                // Normalize direction
                float mag = Mathf.Sqrt(bestDx * bestDx + bestDy * bestDy);
                if (mag > 0f)
                {
                    flowField[i] = new[] { bestDx / mag, bestDy / mag };
                }
                else
                {
                    flowField[i] = new[] { 0f, 0f };
                }

                reachable++;
            }

            // ---- Optional smoothing ----
            if (p.Smoothing)
            {
                float[][] smoothed = new float[totalCells][];
                for (int i = 0; i < totalCells; i++)
                {
                    if (costField[i] >= float.MaxValue || integrationField[i] >= float.MaxValue ||
                        integrationField[i] == 0f)
                    {
                        smoothed[i] = flowField[i];
                        continue;
                    }

                    int cx = i % p.GridWidth;
                    int cy = i / p.GridWidth;
                    float sumX = flowField[i][0];
                    float sumY = flowField[i][1];
                    int count = 1;

                    for (int d = 0; d < 8; d++)
                    {
                        int nx = cx + dx[d];
                        int ny = cy + dy[d];
                        if (nx < 0 || nx >= p.GridWidth || ny < 0 || ny >= p.GridHeight)
                            continue;
                        int nIdx = ny * p.GridWidth + nx;
                        if (costField[nIdx] >= float.MaxValue || integrationField[nIdx] >= float.MaxValue)
                            continue;
                        sumX += flowField[nIdx][0];
                        sumY += flowField[nIdx][1];
                        count++;
                    }

                    float avgX = sumX / count;
                    float avgY = sumY / count;
                    float mag = Mathf.Sqrt(avgX * avgX + avgY * avgY);
                    smoothed[i] = mag > 0.0001f
                        ? new[] { avgX / mag, avgY / mag }
                        : new[] { 0f, 0f };
                }

                flowField = smoothed;
            }

            // ---- Optional debug visualization ----
            string vizName = null;
            if (p.CreateDebugVisualization)
            {
                float cellSize = p.CellSize > 0f ? p.CellSize : 1f;

                GameObject parent;
                if (!string.IsNullOrEmpty(p.VisualizationParent))
                {
                    var existing = GameObject.Find(p.VisualizationParent);
                    if (existing != null)
                    {
                        parent = existing;
                    }
                    else
                    {
                        parent = new GameObject(p.VisualizationParent);
                        Undo.RegisterCreatedObjectUndo(parent, "Flow Field Visualization");
                    }
                }
                else
                {
                    parent = new GameObject("FlowFieldVisualization");
                    Undo.RegisterCreatedObjectUndo(parent, "Flow Field Visualization");
                }

                vizName = parent.name;

                for (int i = 0; i < totalCells; i++)
                {
                    int cx = i % p.GridWidth;
                    int cy = i / p.GridWidth;
                    Vector3 pos = new Vector3(cx * cellSize, 0f, cy * cellSize);

                    // Grid tile
                    var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    tile.transform.SetParent(parent.transform);
                    tile.transform.position = pos;
                    tile.transform.localScale = new Vector3(cellSize * 0.9f, 0.05f, cellSize * 0.9f);

                    Color tileColor;
                    if (costField[i] >= float.MaxValue) tileColor = Color.black;
                    else if (integrationField[i] == 0f) tileColor = Color.green;
                    else if (integrationField[i] >= float.MaxValue) tileColor = Color.red;
                    else tileColor = Color.Lerp(Color.white, Color.cyan,
                        Mathf.Clamp01(integrationField[i] / (p.GridWidth + p.GridHeight)));

                    var mat = new Material(Shader.Find("Standard"));
                    mat.color = tileColor;
                    tile.GetComponent<Renderer>().sharedMaterial = mat;
                    tile.name = $"Cell_{cx}_{cy}";

                    // Arrow for flow direction
                    float fx = flowField[i][0];
                    float fy = flowField[i][1];
                    if (Mathf.Abs(fx) > 0.001f || Mathf.Abs(fy) > 0.001f)
                    {
                        var arrow = new GameObject($"Arrow_{cx}_{cy}");
                        arrow.transform.SetParent(parent.transform);
                        arrow.transform.position = pos + Vector3.up * 0.1f;

                        var lr = arrow.AddComponent<LineRenderer>();
                        lr.positionCount = 2;
                        lr.startWidth = 0.05f * cellSize;
                        lr.endWidth = 0.02f * cellSize;
                        lr.useWorldSpace = true;
                        lr.SetPosition(0, arrow.transform.position);
                        lr.SetPosition(1, arrow.transform.position +
                            new Vector3(fx, 0f, fy) * cellSize * 0.4f);

                        var arrowMat = new Material(Shader.Find("Standard"));
                        arrowMat.color = Color.yellow;
                        lr.sharedMaterial = arrowMat;
                    }
                }
            }

            return ToolResult<AiFlowFieldResult>.Ok(new AiFlowFieldResult
            {
                GridWidth               = p.GridWidth,
                GridHeight              = p.GridHeight,
                FlowField               = flowField,
                DistanceField           = integrationField,
                ReachableCells          = reachable,
                UnreachableCells        = unreachable,
                VisualizationObjectName = vizName
            });
        }
    }
}

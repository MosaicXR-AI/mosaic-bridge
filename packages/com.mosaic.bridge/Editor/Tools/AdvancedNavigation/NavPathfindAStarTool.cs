using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.AdvancedNavigation
{
    public static class NavPathfindAStarTool
    {
        [MosaicTool("nav/pathfind-astar",
                    "Performs A* pathfinding on a 2D grid and returns the path with optional visualization",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<NavPathfindAStarResult> Execute(NavPathfindAStarParams p)
        {
            if (p.GridWidth <= 0 || p.GridHeight <= 0)
                return ToolResult<NavPathfindAStarResult>.Fail(
                    "GridWidth and GridHeight must be positive", ErrorCodes.INVALID_PARAM);

            if (p.StartX < 0 || p.StartX >= p.GridWidth || p.StartY < 0 || p.StartY >= p.GridHeight)
                return ToolResult<NavPathfindAStarResult>.Fail(
                    "Start position is outside the grid", ErrorCodes.OUT_OF_RANGE);

            if (p.EndX < 0 || p.EndX >= p.GridWidth || p.EndY < 0 || p.EndY >= p.GridHeight)
                return ToolResult<NavPathfindAStarResult>.Fail(
                    "End position is outside the grid", ErrorCodes.OUT_OF_RANGE);

            // Build obstacle set
            var obstacles = new HashSet<long>();
            if (p.Obstacles != null)
            {
                if (p.Obstacles.Length % 2 != 0)
                    return ToolResult<NavPathfindAStarResult>.Fail(
                        "Obstacles must be flat array of x,y pairs (even length)", ErrorCodes.INVALID_PARAM);

                for (int i = 0; i < p.Obstacles.Length; i += 2)
                    obstacles.Add(PackCoord(p.Obstacles[i], p.Obstacles[i + 1]));
            }

            if (obstacles.Contains(PackCoord(p.StartX, p.StartY)))
                return ToolResult<NavPathfindAStarResult>.Fail(
                    "Start position is an obstacle", ErrorCodes.INVALID_PARAM);

            if (obstacles.Contains(PackCoord(p.EndX, p.EndY)))
                return ToolResult<NavPathfindAStarResult>.Fail(
                    "End position is an obstacle", ErrorCodes.INVALID_PARAM);

            // A* implementation
            var openSet = new SortedSet<Node>(Comparer<Node>.Create((a, b) =>
            {
                int cmp = a.fCost.CompareTo(b.fCost);
                if (cmp != 0) return cmp;
                cmp = a.hCost.CompareTo(b.hCost);
                if (cmp != 0) return cmp;
                cmp = a.x.CompareTo(b.x);
                if (cmp != 0) return cmp;
                return a.y.CompareTo(b.y);
            }));

            var gCosts = new Dictionary<long, float>();
            var cameFrom = new Dictionary<long, long>();
            var closedSet = new HashSet<long>();
            int nodesExplored = 0;

            long startKey = PackCoord(p.StartX, p.StartY);
            long endKey = PackCoord(p.EndX, p.EndY);

            gCosts[startKey] = 0;
            float startH = Heuristic(p.StartX, p.StartY, p.EndX, p.EndY);
            openSet.Add(new Node(p.StartX, p.StartY, 0, startH));

            // Direction offsets
            int[][] dirs = p.AllowDiagonal
                ? new[] {
                    new[] {0,1}, new[] {1,0}, new[] {0,-1}, new[] {-1,0},
                    new[] {1,1}, new[] {1,-1}, new[] {-1,1}, new[] {-1,-1}
                  }
                : new[] {
                    new[] {0,1}, new[] {1,0}, new[] {0,-1}, new[] {-1,0}
                  };

            bool found = false;

            while (openSet.Count > 0)
            {
                var current = openSet.Min;
                openSet.Remove(current);

                long currentKey = PackCoord(current.x, current.y);
                if (closedSet.Contains(currentKey)) continue;
                closedSet.Add(currentKey);
                nodesExplored++;

                if (currentKey == endKey) { found = true; break; }

                foreach (var dir in dirs)
                {
                    int nx = current.x + dir[0];
                    int ny = current.y + dir[1];

                    if (nx < 0 || nx >= p.GridWidth || ny < 0 || ny >= p.GridHeight) continue;

                    long nKey = PackCoord(nx, ny);
                    if (closedSet.Contains(nKey) || obstacles.Contains(nKey)) continue;

                    float moveCost = (dir[0] != 0 && dir[1] != 0) ? 1.414f : 1f;
                    float tentativeG = gCosts[currentKey] + moveCost;

                    if (!gCosts.ContainsKey(nKey) || tentativeG < gCosts[nKey])
                    {
                        gCosts[nKey] = tentativeG;
                        cameFrom[nKey] = currentKey;
                        float h = Heuristic(nx, ny, p.EndX, p.EndY);
                        openSet.Add(new Node(nx, ny, tentativeG, h));
                    }
                }
            }

            if (!found)
                return ToolResult<NavPathfindAStarResult>.Fail(
                    "No path found between start and end positions", ErrorCodes.NOT_FOUND);

            // Reconstruct path
            var pathList = new List<int>();
            long traceKey = endKey;
            while (traceKey != startKey)
            {
                int tx, ty;
                UnpackCoord(traceKey, out tx, out ty);
                pathList.Add(tx);
                pathList.Add(ty);
                traceKey = cameFrom[traceKey];
            }
            pathList.Add(p.StartX);
            pathList.Add(p.StartY);
            pathList.Reverse();

            int pathLength = pathList.Count / 2;
            int? visId = null;

            // Visualization
            if (p.CreateVisualization)
            {
                var parent = new GameObject("AStarVisualization");
                Undo.RegisterCreatedObjectUndo(parent, "A* Visualization");

                // Grid
                for (int x = 0; x < p.GridWidth; x++)
                {
                    for (int y = 0; y < p.GridHeight; y++)
                    {
                        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        cube.transform.SetParent(parent.transform);
                        cube.transform.position = new Vector3(x, 0, y);
                        cube.transform.localScale = new Vector3(0.9f, 0.1f, 0.9f);

                        Color color = Color.white;
                        long key = PackCoord(x, y);
                        if (obstacles.Contains(key)) color = Color.black;
                        else if (closedSet.Contains(key)) color = new Color(0.7f, 0.85f, 1f);

                        cube.GetComponent<Renderer>().sharedMaterial = CreateTempMaterial(color);
                        cube.name = $"Cell_{x}_{y}";
                    }
                }

                // Path cubes
                for (int i = 0; i < pathList.Count; i += 2)
                {
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.transform.SetParent(parent.transform);
                    cube.transform.position = new Vector3(pathList[i], 0.15f, pathList[i + 1]);
                    cube.transform.localScale = new Vector3(0.5f, 0.3f, 0.5f);
                    cube.GetComponent<Renderer>().sharedMaterial = CreateTempMaterial(Color.green);
                    cube.name = $"Path_{pathList[i]}_{pathList[i + 1]}";
                }

                // Start and end markers
                var startMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                startMarker.transform.SetParent(parent.transform);
                startMarker.transform.position = new Vector3(p.StartX, 0.5f, p.StartY);
                startMarker.transform.localScale = Vector3.one * 0.5f;
                startMarker.GetComponent<Renderer>().sharedMaterial = CreateTempMaterial(Color.blue);
                startMarker.name = "Start";

                var endMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                endMarker.transform.SetParent(parent.transform);
                endMarker.transform.position = new Vector3(p.EndX, 0.5f, p.EndY);
                endMarker.transform.localScale = Vector3.one * 0.5f;
                endMarker.GetComponent<Renderer>().sharedMaterial = CreateTempMaterial(Color.red);
                endMarker.name = "End";

                visId = parent.GetInstanceID();
            }

            return ToolResult<NavPathfindAStarResult>.Ok(new NavPathfindAStarResult
            {
                Path                 = pathList.ToArray(),
                PathLength           = pathLength,
                NodesExplored        = nodesExplored,
                GameObjectInstanceId = visId
            });
        }

        static float Heuristic(int x1, int y1, int x2, int y2)
        {
            float dx = x2 - x1;
            float dy = y2 - y1;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        static long PackCoord(int x, int y) => ((long)x << 32) | (uint)y;

        static void UnpackCoord(long key, out int x, out int y)
        {
            x = (int)(key >> 32);
            y = (int)(key & 0xFFFFFFFF);
        }

        static Material CreateTempMaterial(Color color)
        {
            var mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            return mat;
        }

        struct Node
        {
            public int x, y;
            public float gCost, hCost, fCost;

            public Node(int x, int y, float g, float h)
            {
                this.x = x;
                this.y = y;
                gCost = g;
                hCost = h;
                fCost = g + h;
            }
        }
    }
}

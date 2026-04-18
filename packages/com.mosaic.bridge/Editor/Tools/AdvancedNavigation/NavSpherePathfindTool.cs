using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.AdvancedNavigation
{
    public static class NavSpherePathfindTool
    {
        [MosaicTool("nav/sphere-pathfind",
                    "Performs A* pathfinding on a sphere surface using lat/lon grid with great-circle distance heuristic",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<NavSpherePathfindResult> Execute(NavSpherePathfindParams p)
        {
            var radius     = p.SphereRadius ?? 10f;
            var resolution = p.Resolution ?? 32;

            if (resolution < 4 || resolution > 256)
                return ToolResult<NavSpherePathfindResult>.Fail(
                    "Resolution must be between 4 and 256", ErrorCodes.OUT_OF_RANGE);

            // Build obstacle set from lat/lon pairs
            var obstacleSet = new HashSet<long>();
            if (p.Obstacles != null)
            {
                if (p.Obstacles.Length % 2 != 0)
                    return ToolResult<NavSpherePathfindResult>.Fail(
                        "Obstacles must be flat array of lat,lon pairs", ErrorCodes.INVALID_PARAM);

                for (int i = 0; i < p.Obstacles.Length; i += 2)
                {
                    int gridRow = LatToRow(p.Obstacles[i], resolution);
                    int gridCol = LonToCol(p.Obstacles[i + 1], resolution);
                    obstacleSet.Add(PackCoord(gridRow, gridCol));
                }
            }

            // Convert start/end to grid
            int startRow = LatToRow(p.StartLat, resolution);
            int startCol = LonToCol(p.StartLon, resolution);
            int endRow   = LatToRow(p.EndLat, resolution);
            int endCol   = LonToCol(p.EndLon, resolution);

            long startKey = PackCoord(startRow, startCol);
            long endKey   = PackCoord(endRow, endCol);

            if (obstacleSet.Contains(startKey))
                return ToolResult<NavSpherePathfindResult>.Fail(
                    "Start position is on an obstacle", ErrorCodes.INVALID_PARAM);
            if (obstacleSet.Contains(endKey))
                return ToolResult<NavSpherePathfindResult>.Fail(
                    "End position is on an obstacle", ErrorCodes.INVALID_PARAM);

            // A* on sphere grid
            var openSet = new SortedSet<SphereNode>(Comparer<SphereNode>.Create((a, b) =>
            {
                int cmp = a.fCost.CompareTo(b.fCost);
                if (cmp != 0) return cmp;
                cmp = a.row.CompareTo(b.row);
                if (cmp != 0) return cmp;
                return a.col.CompareTo(b.col);
            }));

            var gCosts   = new Dictionary<long, float>();
            var cameFrom = new Dictionary<long, long>();
            var closed   = new HashSet<long>();

            gCosts[startKey] = 0;
            float startH = GreatCircleDist(
                RowToLat(startRow, resolution), ColToLon(startCol, resolution),
                RowToLat(endRow, resolution), ColToLon(endCol, resolution), radius);
            openSet.Add(new SphereNode(startRow, startCol, 0, startH));

            bool found = false;

            while (openSet.Count > 0)
            {
                var current = openSet.Min;
                openSet.Remove(current);
                long curKey = PackCoord(current.row, current.col);

                if (closed.Contains(curKey)) continue;
                closed.Add(curKey);

                if (curKey == endKey) { found = true; break; }

                // 8 neighbors
                for (int dr = -1; dr <= 1; dr++)
                {
                    for (int dc = -1; dc <= 1; dc++)
                    {
                        if (dr == 0 && dc == 0) continue;
                        int nr = current.row + dr;
                        int nc = (current.col + dc + resolution) % resolution;

                        if (nr < 0 || nr >= resolution) continue;

                        long nKey = PackCoord(nr, nc);
                        if (closed.Contains(nKey) || obstacleSet.Contains(nKey)) continue;

                        float nLat = RowToLat(nr, resolution);
                        float nLon = ColToLon(nc, resolution);
                        float cLat = RowToLat(current.row, resolution);
                        float cLon = ColToLon(current.col, resolution);

                        float moveCost = GreatCircleDist(cLat, cLon, nLat, nLon, radius);
                        float tentG = gCosts[curKey] + moveCost;

                        if (!gCosts.ContainsKey(nKey) || tentG < gCosts[nKey])
                        {
                            gCosts[nKey] = tentG;
                            cameFrom[nKey] = curKey;
                            float h = GreatCircleDist(nLat, nLon,
                                RowToLat(endRow, resolution), ColToLon(endCol, resolution), radius);
                            openSet.Add(new SphereNode(nr, nc, tentG, h));
                        }
                    }
                }
            }

            if (!found)
                return ToolResult<NavSpherePathfindResult>.Fail(
                    "No path found on sphere surface", ErrorCodes.NOT_FOUND);

            // Reconstruct path as lat/lon pairs
            var pathList = new List<float>();
            long trace = endKey;
            float totalArc = 0;
            float prevLat = 0, prevLon = 0;
            bool first = true;

            var pathKeys = new List<long>();
            while (trace != startKey)
            {
                pathKeys.Add(trace);
                trace = cameFrom[trace];
            }
            pathKeys.Add(startKey);
            pathKeys.Reverse();

            foreach (var key in pathKeys)
            {
                int r, c;
                UnpackCoord(key, out r, out c);
                float lat = RowToLat(r, resolution);
                float lon = ColToLon(c, resolution);
                pathList.Add(lat);
                pathList.Add(lon);

                if (!first)
                    totalArc += GreatCircleDist(prevLat, prevLon, lat, lon, radius);
                prevLat = lat;
                prevLon = lon;
                first = false;
            }

            int? visId = null;

            if (p.CreateVisualization)
            {
                var parent = new GameObject("SpherePathVisualization");
                Undo.RegisterCreatedObjectUndo(parent, "Sphere Path Vis");

                // Sphere
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.SetParent(parent.transform);
                sphere.transform.localScale = Vector3.one * radius * 2f;
                sphere.name = "NavSphere";

                // Path line
                var lineObj = new GameObject("PathLine");
                lineObj.transform.SetParent(parent.transform);
                var lr = lineObj.AddComponent<LineRenderer>();
                lr.positionCount = pathKeys.Count;
                lr.startWidth = radius * 0.02f;
                lr.endWidth = radius * 0.02f;
                lr.material = new Material(Shader.Find("Sprites/Default"));
                lr.startColor = Color.green;
                lr.endColor = Color.green;

                for (int i = 0; i < pathKeys.Count; i++)
                {
                    int r, c;
                    UnpackCoord(pathKeys[i], out r, out c);
                    float lat = RowToLat(r, resolution);
                    float lon = ColToLon(c, resolution);
                    lr.SetPosition(i, LatLonToWorld(lat, lon, radius * 1.01f));
                }

                visId = parent.GetInstanceID();
            }

            return ToolResult<NavSpherePathfindResult>.Ok(new NavSpherePathfindResult
            {
                Path                 = pathList.ToArray(),
                PathLength           = pathKeys.Count,
                ArcDistance           = totalArc,
                GameObjectInstanceId = visId
            });
        }

        static int LatToRow(float lat, int res) => Mathf.Clamp(Mathf.RoundToInt((90f - lat) / 180f * (res - 1)), 0, res - 1);
        static int LonToCol(float lon, int res) => ((Mathf.RoundToInt((lon + 180f) / 360f * res) % res) + res) % res;
        static float RowToLat(int row, int res) => 90f - (float)row / (res - 1) * 180f;
        static float ColToLon(int col, int res) => (float)col / res * 360f - 180f;

        static float GreatCircleDist(float lat1, float lon1, float lat2, float lon2, float radius)
        {
            float la1 = lat1 * Mathf.Deg2Rad;
            float la2 = lat2 * Mathf.Deg2Rad;
            float dLat = (lat2 - lat1) * Mathf.Deg2Rad;
            float dLon = (lon2 - lon1) * Mathf.Deg2Rad;
            float a = Mathf.Sin(dLat / 2f) * Mathf.Sin(dLat / 2f) +
                      Mathf.Cos(la1) * Mathf.Cos(la2) *
                      Mathf.Sin(dLon / 2f) * Mathf.Sin(dLon / 2f);
            float c = 2f * Mathf.Atan2(Mathf.Sqrt(a), Mathf.Sqrt(1f - a));
            return radius * c;
        }

        static Vector3 LatLonToWorld(float lat, float lon, float radius)
        {
            float la = lat * Mathf.Deg2Rad;
            float lo = lon * Mathf.Deg2Rad;
            return new Vector3(
                radius * Mathf.Cos(la) * Mathf.Cos(lo),
                radius * Mathf.Sin(la),
                radius * Mathf.Cos(la) * Mathf.Sin(lo));
        }

        static long PackCoord(int r, int c) => ((long)r << 32) | (uint)c;
        static void UnpackCoord(long key, out int r, out int c) { r = (int)(key >> 32); c = (int)(key & 0xFFFFFFFF); }

        struct SphereNode
        {
            public int row, col;
            public float gCost, hCost, fCost;
            public SphereNode(int r, int c, float g, float h) { row = r; col = c; gCost = g; hCost = h; fCost = g + h; }
        }
    }
}

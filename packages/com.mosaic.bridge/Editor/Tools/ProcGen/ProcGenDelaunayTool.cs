using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.ProcGen
{
    public static class ProcGenDelaunayTool
    {
        [MosaicTool("procgen/delaunay",
                    "Generates a Delaunay triangulation from 2D points using the Bowyer-Watson algorithm",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<ProcGenDelaunayResult> Execute(ProcGenDelaunayParams p)
        {
            if (p.Points == null || p.Points.Length < 3)
                return ToolResult<ProcGenDelaunayResult>.Fail(
                    "At least 3 points are required for triangulation",
                    ErrorCodes.INVALID_PARAM);

            var points = new List<Vector2>(p.Points.Length);
            foreach (var pt in p.Points)
            {
                if (pt == null || pt.Length < 2)
                    return ToolResult<ProcGenDelaunayResult>.Fail(
                        "Each point must be a float array with at least 2 elements [x,y]",
                        ErrorCodes.INVALID_PARAM);
                points.Add(new Vector2(pt[0], pt[1]));
            }

            // Check for duplicate points
            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    if (Vector2.Distance(points[i], points[j]) < 1e-6f)
                        return ToolResult<ProcGenDelaunayResult>.Fail(
                            $"Duplicate points found at indices {i} and {j}",
                            ErrorCodes.INVALID_PARAM);
                }
            }

            List<int> triangles;
            try
            {
                triangles = BowyerWatson(points);
            }
            catch (Exception ex)
            {
                return ToolResult<ProcGenDelaunayResult>.Fail(
                    $"Triangulation failed: {ex.Message}", ErrorCodes.INTERNAL_ERROR);
            }

            bool createMesh = p.CreateMesh ?? true;
            float meshY = p.MeshHeight ?? 0f;
            string meshPath = null;
            string goName = null;

            if (createMesh)
            {
                var vertices = new Vector3[points.Count];
                for (int i = 0; i < points.Count; i++)
                    vertices[i] = new Vector3(points[i].x, meshY, points[i].y);

                var mesh = new Mesh { name = "DelaunayMesh" };
                mesh.vertices = vertices;
                mesh.triangles = triangles.ToArray();
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                string savePath = p.SavePath ?? "Assets/Generated/ProcGen/Delaunay";
                if (!savePath.StartsWith("Assets/"))
                    return ToolResult<ProcGenDelaunayResult>.Fail(
                        "SavePath must start with 'Assets/'", ErrorCodes.INVALID_PARAM);

                var fullDir = Path.Combine(Application.dataPath, "..", savePath);
                Directory.CreateDirectory(fullDir);

                meshPath = savePath + "/DelaunayMesh.asset";
                AssetDatabase.CreateAsset(mesh, meshPath);
                AssetDatabase.SaveAssets();

                var go = new GameObject("DelaunayMesh");
                Undo.RegisterCreatedObjectUndo(go, "Create Delaunay Mesh");
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>(
                    "Default-Diffuse.mat");
                goName = go.name;
            }

            return ToolResult<ProcGenDelaunayResult>.Ok(new ProcGenDelaunayResult
            {
                TriangleCount  = triangles.Count / 3,
                VertexCount    = points.Count,
                Triangles      = triangles.ToArray(),
                MeshPath       = meshPath,
                GameObjectName = goName
            });
        }

        // --- Bowyer-Watson algorithm ---
        internal static List<int> BowyerWatson(List<Vector2> points)
        {
            // Build working list with super-triangle vertices appended
            var allPoints = new List<Vector2>(points);

            // Compute bounding box
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            foreach (var pt in points)
            {
                if (pt.x < minX) minX = pt.x;
                if (pt.y < minY) minY = pt.y;
                if (pt.x > maxX) maxX = pt.x;
                if (pt.y > maxY) maxY = pt.y;
            }

            float dx = maxX - minX;
            float dy = maxY - minY;
            float deltaMax = Mathf.Max(dx, dy);
            float midX = (minX + maxX) * 0.5f;
            float midY = (minY + maxY) * 0.5f;

            // Super-triangle vertices (large enough to contain all points)
            int st0 = allPoints.Count;
            allPoints.Add(new Vector2(midX - 20 * deltaMax, midY - deltaMax));
            int st1 = allPoints.Count;
            allPoints.Add(new Vector2(midX, midY + 20 * deltaMax));
            int st2 = allPoints.Count;
            allPoints.Add(new Vector2(midX + 20 * deltaMax, midY - deltaMax));

            // Triangle list: each triangle is 3 vertex indices
            var tris = new List<int[]> { new[] { st0, st1, st2 } };

            // Insert each point
            for (int i = 0; i < points.Count; i++)
            {
                var pt = points[i];
                var badTriangles = new List<int>();

                for (int t = 0; t < tris.Count; t++)
                {
                    var tri = tris[t];
                    if (InCircumcircle(pt, allPoints[tri[0]], allPoints[tri[1]], allPoints[tri[2]]))
                        badTriangles.Add(t);
                }

                // Find boundary polygon (edges that are not shared by multiple bad triangles)
                var polygon = new List<int[]>();
                for (int bi = 0; bi < badTriangles.Count; bi++)
                {
                    var tri = tris[badTriangles[bi]];
                    for (int e = 0; e < 3; e++)
                    {
                        int ea = tri[e];
                        int eb = tri[(e + 1) % 3];
                        bool shared = false;
                        for (int bj = 0; bj < badTriangles.Count; bj++)
                        {
                            if (bi == bj) continue;
                            var otherTri = tris[badTriangles[bj]];
                            if (EdgeInTriangle(ea, eb, otherTri))
                            {
                                shared = true;
                                break;
                            }
                        }
                        if (!shared)
                            polygon.Add(new[] { ea, eb });
                    }
                }

                // Remove bad triangles (in reverse order to preserve indices)
                badTriangles.Sort();
                for (int b = badTriangles.Count - 1; b >= 0; b--)
                    tris.RemoveAt(badTriangles[b]);

                // Re-triangulate with the new point
                foreach (var edge in polygon)
                    tris.Add(new[] { edge[0], edge[1], i });
            }

            // Remove triangles that reference super-triangle vertices
            var result = new List<int>();
            foreach (var tri in tris)
            {
                if (tri[0] >= st0 || tri[1] >= st0 || tri[2] >= st0)
                    continue;

                // Ensure consistent winding (CCW)
                var a = allPoints[tri[0]];
                var b = allPoints[tri[1]];
                var c = allPoints[tri[2]];
                float cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                if (cross > 0)
                {
                    result.Add(tri[0]);
                    result.Add(tri[1]);
                    result.Add(tri[2]);
                }
                else
                {
                    result.Add(tri[0]);
                    result.Add(tri[2]);
                    result.Add(tri[1]);
                }
            }

            return result;
        }

        static bool InCircumcircle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float ax = a.x - p.x, ay = a.y - p.y;
            float bx = b.x - p.x, by = b.y - p.y;
            float cx = c.x - p.x, cy = c.y - p.y;

            float det = ax * (by * (cx * cx + cy * cy) - cy * (bx * bx + by * by))
                      - bx * (ay * (cx * cx + cy * cy) - cy * (ax * ax + ay * ay))
                      + cx * (ay * (bx * bx + by * by) - by * (ax * ax + ay * ay));

            // If triangle is CW, det sign flips
            float triArea = (a.x - c.x) * (b.y - c.y) - (a.y - c.y) * (b.x - c.x);
            return triArea > 0 ? det > 0 : det < 0;
        }

        static bool EdgeInTriangle(int ea, int eb, int[] tri)
        {
            for (int e = 0; e < 3; e++)
            {
                int ta = tri[e];
                int tb = tri[(e + 1) % 3];
                if ((ta == ea && tb == eb) || (ta == eb && tb == ea))
                    return true;
            }
            return false;
        }
    }
}

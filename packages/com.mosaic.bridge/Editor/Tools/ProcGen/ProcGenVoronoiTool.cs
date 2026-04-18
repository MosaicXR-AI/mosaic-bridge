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
    public static class ProcGenVoronoiTool
    {
        [MosaicTool("procgen/voronoi",
                    "Generates a Voronoi diagram with optional Lloyd relaxation, mesh, or texture output",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<ProcGenVoronoiResult> Execute(ProcGenVoronoiParams p)
        {
            // --- Validate bounds ---
            if (p.BoundsMin == null || p.BoundsMin.Length < 2)
                return ToolResult<ProcGenVoronoiResult>.Fail(
                    "BoundsMin must be a float array with at least 2 elements [x,y]",
                    ErrorCodes.INVALID_PARAM);

            if (p.BoundsMax == null || p.BoundsMax.Length < 2)
                return ToolResult<ProcGenVoronoiResult>.Fail(
                    "BoundsMax must be a float array with at least 2 elements [x,y]",
                    ErrorCodes.INVALID_PARAM);

            float minX = p.BoundsMin[0];
            float minY = p.BoundsMin[1];
            float maxX = p.BoundsMax[0];
            float maxY = p.BoundsMax[1];

            if (maxX <= minX || maxY <= minY)
                return ToolResult<ProcGenVoronoiResult>.Fail(
                    "BoundsMax must be greater than BoundsMin in both dimensions",
                    ErrorCodes.INVALID_PARAM);

            // --- Resolve points ---
            List<Vector2> points;
            if (p.Points != null && p.Points.Length > 0)
            {
                points = new List<Vector2>(p.Points.Length);
                foreach (var pt in p.Points)
                {
                    if (pt == null || pt.Length < 2)
                        return ToolResult<ProcGenVoronoiResult>.Fail(
                            "Each point must be a float array with at least 2 elements [x,y]",
                            ErrorCodes.INVALID_PARAM);
                    points.Add(new Vector2(pt[0], pt[1]));
                }
            }
            else
            {
                int count = p.PointCount ?? 20;
                if (count < 2)
                    return ToolResult<ProcGenVoronoiResult>.Fail(
                        "PointCount must be at least 2", ErrorCodes.INVALID_PARAM);

                int seed = p.Seed ?? Environment.TickCount;
                var rng = new System.Random(seed);
                points = new List<Vector2>(count);
                for (int i = 0; i < count; i++)
                {
                    float x = minX + (float)(rng.NextDouble() * (maxX - minX));
                    float y = minY + (float)(rng.NextDouble() * (maxY - minY));
                    points.Add(new Vector2(x, y));
                }
            }

            // --- Lloyd relaxation ---
            int relaxIter = p.RelaxIterations ?? 0;
            for (int iter = 0; iter < relaxIter; iter++)
                points = LloydRelax(points, minX, minY, maxX, maxY);

            // --- Build cell info using nearest-neighbor relationships ---
            var cells = BuildCellInfo(points, minX, minY, maxX, maxY);

            string output = (p.Output ?? "diagram").ToLowerInvariant();
            string meshPath = null;
            string texturePath = null;

            // --- Texture output ---
            if (output == "texture")
            {
                int res = p.TextureResolution ?? 512;
                if (res < 1 || res > 8192)
                    return ToolResult<ProcGenVoronoiResult>.Fail(
                        "TextureResolution must be between 1 and 8192",
                        ErrorCodes.INVALID_PARAM);

                var tex = RenderVoronoiTexture(points, minX, minY, maxX, maxY, res);
                string savePath = p.SavePath ?? "Assets/Generated/ProcGen/Voronoi";
                if (!savePath.StartsWith("Assets/"))
                    return ToolResult<ProcGenVoronoiResult>.Fail(
                        "SavePath must start with 'Assets/'", ErrorCodes.INVALID_PARAM);

                var fullDir = Path.Combine(Application.dataPath, "..", savePath);
                Directory.CreateDirectory(fullDir);

                string texFile = Path.Combine(fullDir, "VoronoiDiagram.png");
                File.WriteAllBytes(texFile, tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);

                texturePath = savePath + "/VoronoiDiagram.png";
                AssetDatabase.ImportAsset(texturePath);
            }

            // --- Mesh output ---
            if (output == "mesh")
            {
                string savePath = p.SavePath ?? "Assets/Generated/ProcGen/Voronoi";
                if (!savePath.StartsWith("Assets/"))
                    return ToolResult<ProcGenVoronoiResult>.Fail(
                        "SavePath must start with 'Assets/'", ErrorCodes.INVALID_PARAM);

                var fullDir = Path.Combine(Application.dataPath, "..", savePath);
                Directory.CreateDirectory(fullDir);

                // Build mesh from Delaunay triangulation of the seed points
                var mesh = BuildVoronoiMesh(points, minX, minY, maxX, maxY);
                string meshFile = savePath + "/VoronoiMesh.asset";
                AssetDatabase.CreateAsset(mesh, meshFile);
                AssetDatabase.SaveAssets();
                meshPath = meshFile;

                if (p.CreateGameObjects == true)
                {
                    var go = new GameObject("VoronoiMesh");
                    Undo.RegisterCreatedObjectUndo(go, "Create Voronoi Mesh");
                    var mf = go.AddComponent<MeshFilter>();
                    mf.sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>(
                        "Default-Diffuse.mat");
                }
            }

            // --- CreateGameObjects for diagram mode ---
            if (output == "diagram" && p.CreateGameObjects == true)
            {
                var parent = new GameObject("VoronoiDiagram");
                Undo.RegisterCreatedObjectUndo(parent, "Create Voronoi Diagram");
                for (int i = 0; i < points.Count; i++)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.name = $"VoronoiSeed_{i}";
                    go.transform.position = new Vector3(points[i].x, 0, points[i].y);
                    go.transform.localScale = Vector3.one * 0.3f;
                    go.transform.SetParent(parent.transform);
                    Undo.RegisterCreatedObjectUndo(go, "Create Voronoi Seed");
                }
            }

            var cellInfos = new VoronoiCellInfo[cells.Count];
            for (int i = 0; i < cells.Count; i++)
                cellInfos[i] = cells[i];

            return ToolResult<ProcGenVoronoiResult>.Ok(new ProcGenVoronoiResult
            {
                CellCount   = points.Count,
                Cells       = cellInfos,
                MeshPath    = meshPath,
                TexturePath = texturePath
            });
        }

        // --- Lloyd relaxation: move each point to the centroid of its Voronoi cell ---
        internal static List<Vector2> LloydRelax(List<Vector2> points,
            float minX, float minY, float maxX, float maxY)
        {
            // Approximate centroids via sampling
            int sampleRes = 128;
            float stepX = (maxX - minX) / sampleRes;
            float stepY = (maxY - minY) / sampleRes;

            var centroids = new Vector2[points.Count];
            var counts = new int[points.Count];

            for (int sy = 0; sy < sampleRes; sy++)
            {
                for (int sx = 0; sx < sampleRes; sx++)
                {
                    float px = minX + (sx + 0.5f) * stepX;
                    float py = minY + (sy + 0.5f) * stepY;
                    int nearest = FindNearest(points, px, py);
                    centroids[nearest] += new Vector2(px, py);
                    counts[nearest]++;
                }
            }

            var relaxed = new List<Vector2>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                if (counts[i] > 0)
                    relaxed.Add(centroids[i] / counts[i]);
                else
                    relaxed.Add(points[i]);
            }
            return relaxed;
        }

        internal static int FindNearest(List<Vector2> points, float x, float y)
        {
            int best = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                float dx = points[i].x - x;
                float dy = points[i].y - y;
                float d = dx * dx + dy * dy;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }
            return best;
        }

        static List<VoronoiCellInfo> BuildCellInfo(List<Vector2> points,
            float minX, float minY, float maxX, float maxY)
        {
            // Compute neighbor relationships via proximity
            var neighbors = new HashSet<int>[points.Count];
            for (int i = 0; i < points.Count; i++)
                neighbors[i] = new HashSet<int>();

            // Use grid sampling to find adjacent cells
            int sampleRes = 128;
            float stepX = (maxX - minX) / sampleRes;
            float stepY = (maxY - minY) / sampleRes;
            var grid = new int[sampleRes, sampleRes];

            for (int sy = 0; sy < sampleRes; sy++)
            {
                for (int sx = 0; sx < sampleRes; sx++)
                {
                    float px = minX + (sx + 0.5f) * stepX;
                    float py = minY + (sy + 0.5f) * stepY;
                    grid[sx, sy] = FindNearest(points, px, py);
                }
            }

            // Find neighbors by checking adjacent grid cells
            for (int sy = 0; sy < sampleRes; sy++)
            {
                for (int sx = 0; sx < sampleRes; sx++)
                {
                    int cellId = grid[sx, sy];
                    if (sx + 1 < sampleRes && grid[sx + 1, sy] != cellId)
                    {
                        neighbors[cellId].Add(grid[sx + 1, sy]);
                        neighbors[grid[sx + 1, sy]].Add(cellId);
                    }
                    if (sy + 1 < sampleRes && grid[sx, sy + 1] != cellId)
                    {
                        neighbors[cellId].Add(grid[sx, sy + 1]);
                        neighbors[grid[sx, sy + 1]].Add(cellId);
                    }
                }
            }

            // Estimate vertex count from neighbor count (each cell vertex is shared by 3 cells)
            var result = new List<VoronoiCellInfo>(points.Count);
            for (int i = 0; i < points.Count; i++)
            {
                result.Add(new VoronoiCellInfo
                {
                    Center        = new[] { points[i].x, points[i].y },
                    VertexCount   = neighbors[i].Count,
                    NeighborCount = neighbors[i].Count
                });
            }
            return result;
        }

        static Texture2D RenderVoronoiTexture(List<Vector2> points,
            float minX, float minY, float maxX, float maxY, int res)
        {
            var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
            var colors = GenerateCellColors(points.Count);
            float rangeX = maxX - minX;
            float rangeY = maxY - minY;

            var pixels = new Color[res * res];
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float px = minX + (x + 0.5f) / res * rangeX;
                    float py = minY + (y + 0.5f) / res * rangeY;
                    int nearest = FindNearest(points, px, py);
                    pixels[y * res + x] = colors[nearest];
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        static Color[] GenerateCellColors(int count)
        {
            var colors = new Color[count];
            for (int i = 0; i < count; i++)
            {
                // Use golden-ratio hue spread for distinct colors
                float hue = (i * 0.618033988749895f) % 1.0f;
                colors[i] = Color.HSVToRGB(hue, 0.7f, 0.9f);
            }
            return colors;
        }

        static Mesh BuildVoronoiMesh(List<Vector2> points,
            float minX, float minY, float maxX, float maxY)
        {
            // Use Delaunay triangulation of the seed points to build a mesh
            var triangles = ProcGenDelaunayTool.BowyerWatson(points);

            var vertices = new Vector3[points.Count];
            for (int i = 0; i < points.Count; i++)
                vertices[i] = new Vector3(points[i].x, 0, points[i].y);

            var mesh = new Mesh { name = "VoronoiMesh" };
            mesh.vertices = vertices;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.AdvancedMesh
{
    public static class MeshTriangulateTool
    {
        [MosaicTool("mesh/triangulate",
                    "Triangulates a 2D polygon using the ear-clipping algorithm and saves as a mesh asset",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<MeshTriangulateResult> Execute(MeshTriangulateParams p)
        {
            if (p.Points == null || p.Points.Length < 6)
                return ToolResult<MeshTriangulateResult>.Fail(
                    "Points must contain at least 6 floats (3 x,y pairs)", ErrorCodes.INVALID_PARAM);

            if (p.Points.Length % 2 != 0)
                return ToolResult<MeshTriangulateResult>.Fail(
                    "Points array length must be even (x,y pairs)", ErrorCodes.INVALID_PARAM);

            int vertexCount = p.Points.Length / 2;
            var polygon = new List<Vector2>(vertexCount);
            for (int i = 0; i < vertexCount; i++)
                polygon.Add(new Vector2(p.Points[i * 2], p.Points[i * 2 + 1]));

            // Ensure counter-clockwise winding
            if (SignedArea(polygon) < 0)
                polygon.Reverse();

            // Ear-clipping triangulation
            var indices = new List<int>();
            var remaining = new List<int>(vertexCount);
            for (int i = 0; i < vertexCount; i++)
                remaining.Add(i);

            int safety = vertexCount * vertexCount;
            while (remaining.Count > 2 && safety-- > 0)
            {
                bool earFound = false;
                for (int i = 0; i < remaining.Count; i++)
                {
                    int prev = remaining[(i - 1 + remaining.Count) % remaining.Count];
                    int curr = remaining[i];
                    int next = remaining[(i + 1) % remaining.Count];

                    if (!IsConvex(polygon[prev], polygon[curr], polygon[next]))
                        continue;

                    bool containsOther = false;
                    for (int j = 0; j < remaining.Count; j++)
                    {
                        int idx = remaining[j];
                        if (idx == prev || idx == curr || idx == next) continue;
                        if (PointInTriangle(polygon[idx], polygon[prev], polygon[curr], polygon[next]))
                        {
                            containsOther = true;
                            break;
                        }
                    }

                    if (!containsOther)
                    {
                        indices.Add(prev);
                        indices.Add(curr);
                        indices.Add(next);
                        remaining.RemoveAt(i);
                        earFound = true;
                        break;
                    }
                }

                if (!earFound)
                    break;
            }

            // Build mesh
            var vertices = new Vector3[vertexCount];
            for (int i = 0; i < vertexCount; i++)
                vertices[i] = new Vector3(polygon[i].x, polygon[i].y, 0f);

            var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(p.OutputPath) };
            mesh.vertices = vertices;
            mesh.triangles = indices.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // Save asset
            string absoluteDir = Path.GetDirectoryName(
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", p.OutputPath)));
            if (!string.IsNullOrEmpty(absoluteDir))
                Directory.CreateDirectory(absoluteDir);

            AssetDatabase.CreateAsset(mesh, p.OutputPath);
            AssetDatabase.SaveAssets();

            int? goId = null;
            if (p.CreateGameObject)
            {
                var go = new GameObject(mesh.name);
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                go.AddComponent<MeshRenderer>();
                Undo.RegisterCreatedObjectUndo(go, "Create Triangulated Mesh");
                goId = go.GetInstanceID();
            }

            return ToolResult<MeshTriangulateResult>.Ok(new MeshTriangulateResult
            {
                OutputPath = p.OutputPath,
                VertexCount = vertexCount,
                TriangleCount = indices.Count / 3,
                GameObjectInstanceId = goId
            });
        }

        static float SignedArea(List<Vector2> polygon)
        {
            float area = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i];
                var b = polygon[(i + 1) % polygon.Count];
                area += (b.x - a.x) * (b.y + a.y);
            }
            return area * 0.5f;
        }

        static bool IsConvex(Vector2 a, Vector2 b, Vector2 c)
        {
            return Cross(b - a, c - a) > 0f;
        }

        static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a);
            float d2 = Cross(c - b, p - b);
            float d3 = Cross(a - c, p - c);

            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

            return !(hasNeg && hasPos);
        }
    }
}

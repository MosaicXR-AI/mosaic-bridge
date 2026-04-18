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
    public static class MeshSphereSubdivideTool
    {
        [MosaicTool("mesh/sphere-subdivide",
                    "Generates an icosphere by subdividing an icosahedron to the specified level",
                    isReadOnly: false, Context = ToolContext.Both)]
        public static ToolResult<MeshSphereSubdivideResult> Execute(MeshSphereSubdivideParams p)
        {
            int subdivisions = Mathf.Clamp(p.Subdivisions, 0, 6);
            float radius = Mathf.Max(0.001f, p.Radius);

            // Golden ratio
            float t = (1f + Mathf.Sqrt(5f)) / 2f;

            var vertices = new List<Vector3>
            {
                new Vector3(-1,  t,  0).normalized,
                new Vector3( 1,  t,  0).normalized,
                new Vector3(-1, -t,  0).normalized,
                new Vector3( 1, -t,  0).normalized,
                new Vector3( 0, -1,  t).normalized,
                new Vector3( 0,  1,  t).normalized,
                new Vector3( 0, -1, -t).normalized,
                new Vector3( 0,  1, -t).normalized,
                new Vector3( t,  0, -1).normalized,
                new Vector3( t,  0,  1).normalized,
                new Vector3(-t,  0, -1).normalized,
                new Vector3(-t,  0,  1).normalized,
            };

            var triangles = new List<int>
            {
                0,11,5,   0,5,1,    0,1,7,    0,7,10,   0,10,11,
                1,5,9,    5,11,4,   11,10,2,  10,7,6,   7,1,8,
                3,9,4,    3,4,2,    3,2,6,    3,6,8,    3,8,9,
                4,9,5,    2,4,11,   6,2,10,   8,6,7,    9,8,1,
            };

            // Subdivide
            var midpointCache = new Dictionary<long, int>();

            for (int s = 0; s < subdivisions; s++)
            {
                var newTriangles = new List<int>();
                midpointCache.Clear();

                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int v0 = triangles[i];
                    int v1 = triangles[i + 1];
                    int v2 = triangles[i + 2];

                    int a = GetMidpoint(v0, v1, vertices, midpointCache);
                    int b = GetMidpoint(v1, v2, vertices, midpointCache);
                    int c = GetMidpoint(v2, v0, vertices, midpointCache);

                    newTriangles.Add(v0); newTriangles.Add(a); newTriangles.Add(c);
                    newTriangles.Add(v1); newTriangles.Add(b); newTriangles.Add(a);
                    newTriangles.Add(v2); newTriangles.Add(c); newTriangles.Add(b);
                    newTriangles.Add(a);  newTriangles.Add(b); newTriangles.Add(c);
                }

                triangles = newTriangles;
            }

            // Scale by radius and compute UVs/normals
            var normals = new Vector3[vertices.Count];
            var uvs = new Vector2[vertices.Count];

            for (int i = 0; i < vertices.Count; i++)
            {
                normals[i] = vertices[i]; // already normalized
                vertices[i] = vertices[i] * radius;

                // Spherical UV mapping
                float lon = Mathf.Atan2(normals[i].z, normals[i].x);
                float lat = Mathf.Asin(Mathf.Clamp(normals[i].y, -1f, 1f));
                uvs[i] = new Vector2(
                    0.5f + lon / (2f * Mathf.PI),
                    0.5f + lat / Mathf.PI);
            }

            var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(p.OutputPath) };
            if (vertices.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(new List<Vector3>(normals));
            mesh.SetUVs(0, new List<Vector2>(uvs));
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();

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
                Undo.RegisterCreatedObjectUndo(go, "Create Icosphere");
                goId = go.GetInstanceID();
            }

            return ToolResult<MeshSphereSubdivideResult>.Ok(new MeshSphereSubdivideResult
            {
                OutputPath = p.OutputPath,
                VertexCount = vertices.Count,
                TriangleCount = triangles.Count / 3,
                Subdivisions = subdivisions,
                Radius = radius,
                GameObjectInstanceId = goId
            });
        }

        static int GetMidpoint(int i1, int i2, List<Vector3> vertices, Dictionary<long, int> cache)
        {
            long smallerIndex = Math.Min(i1, i2);
            long greaterIndex = Math.Max(i1, i2);
            long key = (smallerIndex << 32) + greaterIndex;

            if (cache.TryGetValue(key, out int ret))
                return ret;

            Vector3 mid = ((vertices[i1] + vertices[i2]) * 0.5f).normalized;
            int idx = vertices.Count;
            vertices.Add(mid);
            cache[key] = idx;
            return idx;
        }
    }
}

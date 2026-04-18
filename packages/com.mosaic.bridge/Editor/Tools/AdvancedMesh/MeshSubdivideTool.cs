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
    /// <summary>
    /// Subdivides a triangle mesh using Loop, sqrt(3) (barycentric), or an approximation of
    /// Catmull-Clark (implemented via Loop over triangles — acceptable MVP per story spec).
    /// Each iteration roughly quadruples triangle count for Loop/Catmull-Clark.
    /// </summary>
    public static class MeshSubdivideTool
    {
        private static readonly HashSet<string> ValidMethods = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        { "catmull_clark", "loop", "sqrt3" };

        [MosaicTool("mesh/subdivide",
                    "Subdivides a mesh using Loop, Catmull-Clark (triangle approximation), or sqrt(3).",
                    isReadOnly: false, category: "mesh", Context = ToolContext.Both)]
        public static ToolResult<MeshSubdivideResult> Execute(MeshSubdivideParams p)
        {
            if (p == null)
                return ToolResult<MeshSubdivideResult>.Fail(
                    "Params cannot be null.", ErrorCodes.INVALID_PARAM);

            string method = (p.Method ?? "catmull_clark").ToLowerInvariant();
            if (!ValidMethods.Contains(method))
                return ToolResult<MeshSubdivideResult>.Fail(
                    $"Invalid Method '{p.Method}'. Expected 'catmull_clark', 'loop', or 'sqrt3'.",
                    ErrorCodes.INVALID_PARAM);

            int iterations = Mathf.Clamp(p.Iterations <= 0 ? 1 : p.Iterations, 1, 4);

            // ── Resolve source mesh ───────────────────────────────────────
            Mesh source = null;
            string sourceName = null;

            if (!string.IsNullOrEmpty(p.SourceMeshPath))
            {
                source = AssetDatabase.LoadAssetAtPath<Mesh>(p.SourceMeshPath);
                if (source == null)
                    return ToolResult<MeshSubdivideResult>.Fail(
                        $"Mesh asset not found at '{p.SourceMeshPath}'.", ErrorCodes.NOT_FOUND);
                sourceName = Path.GetFileNameWithoutExtension(p.SourceMeshPath);
            }
            else if (!string.IsNullOrEmpty(p.GameObjectName))
            {
                var go = GameObject.Find(p.GameObjectName);
                if (go == null)
                    return ToolResult<MeshSubdivideResult>.Fail(
                        $"GameObject '{p.GameObjectName}' not found in scene.", ErrorCodes.NOT_FOUND);
                var mf = go.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null)
                    return ToolResult<MeshSubdivideResult>.Fail(
                        $"GameObject '{p.GameObjectName}' has no MeshFilter with a sharedMesh.",
                        ErrorCodes.NOT_FOUND);
                source = mf.sharedMesh;
                sourceName = go.name;
            }
            else
            {
                return ToolResult<MeshSubdivideResult>.Fail(
                    "Either SourceMeshPath or GameObjectName must be provided.",
                    ErrorCodes.INVALID_PARAM);
            }

            string savePath = string.IsNullOrEmpty(p.SavePath) ? "Assets/Generated/Mesh/" : p.SavePath;
            if (!savePath.StartsWith("Assets/"))
                return ToolResult<MeshSubdivideResult>.Fail(
                    "SavePath must start with 'Assets/'.", ErrorCodes.INVALID_PARAM);
            if (!savePath.EndsWith("/")) savePath += "/";

            // ── Extract vertices/triangles from source ────────────────────
            Vector3[] srcVerts = source.vertices;
            int[] srcTris = source.triangles;

            int originalVertexCount = srcVerts.Length;
            int originalTriangleCount = srcTris.Length / 3;

            var verts = new List<Vector3>(srcVerts);
            var tris = new List<int>(srcTris);

            // ── Subdivide ─────────────────────────────────────────────────
            try
            {
                for (int it = 0; it < iterations; it++)
                {
                    switch (method)
                    {
                        case "loop":
                        case "catmull_clark": // MVP approximation via Loop on triangles
                            LoopSubdivide(verts, tris, p.PreserveCreases);
                            break;
                        case "sqrt3":
                            Sqrt3Subdivide(verts, tris, p.PreserveCreases);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                return ToolResult<MeshSubdivideResult>.Fail(
                    $"Subdivision failed: {ex.Message}", ErrorCodes.INVALID_PARAM);
            }

            // ── Build mesh ────────────────────────────────────────────────
            string outputName = string.IsNullOrEmpty(p.OutputName)
                ? (sourceName + "_Subdiv")
                : p.OutputName;

            var mesh = new Mesh { name = outputName };
            if (verts.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // ── Save as .asset ────────────────────────────────────────────
            string absoluteDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", savePath));
            Directory.CreateDirectory(absoluteDir);

            string assetPath = savePath + outputName + ".asset";
            AssetDatabase.CreateAsset(mesh, assetPath);
            AssetDatabase.SaveAssets();

            return ToolResult<MeshSubdivideResult>.Ok(new MeshSubdivideResult
            {
                MeshPath = assetPath,
                Method = method,
                Iterations = iterations,
                OriginalVertexCount = originalVertexCount,
                OriginalTriangleCount = originalTriangleCount,
                NewVertexCount = verts.Count,
                NewTriangleCount = tris.Count / 3
            });
        }

        // ═══════════════════════════════════════════════════════════════════
        // Loop subdivision
        //
        // Per iteration:
        //   1. Compute new vertex positions with Loop's vertex mask
        //      β = (5/8 - (3/8 + cos(2π/n)/4)²) / n
        //      V_new = (1 - nβ) V_old + β Σ neighbors
        //   2. Compute edge points. Interior: 3/8 (V1+V2) + 1/8 (VL+VR)
        //      Boundary: 1/2 (V1+V2).
        //   3. Replace each triangle with 4 triangles.
        //
        // PreserveCreases=true keeps boundary vertices at their original positions.
        // ═══════════════════════════════════════════════════════════════════
        internal static void LoopSubdivide(List<Vector3> verts, List<int> tris, bool preserveCreases)
        {
            int vCount = verts.Count;
            int triCount = tris.Count / 3;

            // Build edge → adjacent triangle list.
            // Each edge stores up to two opposite vertices (third vertex of the triangle).
            var edgeAdj = new Dictionary<long, List<int>>();        // edge key → list of opposite vtx
            var edgeMidpointIdx = new Dictionary<long, int>();      // edge key → index of new midpoint vertex

            // Vertex neighbors (set of adjacent vertex indices)
            var neighbors = new HashSet<int>[vCount];
            for (int i = 0; i < vCount; i++) neighbors[i] = new HashSet<int>();

            // Boundary flag per vertex
            var isBoundary = new bool[vCount];

            for (int t = 0; t < triCount; t++)
            {
                int a = tris[t * 3];
                int b = tris[t * 3 + 1];
                int c = tris[t * 3 + 2];

                AddEdge(edgeAdj, a, b, c);
                AddEdge(edgeAdj, b, c, a);
                AddEdge(edgeAdj, c, a, b);

                neighbors[a].Add(b); neighbors[a].Add(c);
                neighbors[b].Add(a); neighbors[b].Add(c);
                neighbors[c].Add(a); neighbors[c].Add(b);
            }

            // Mark boundary edges/vertices (edges with only one adjacent triangle)
            foreach (var kv in edgeAdj)
            {
                if (kv.Value.Count == 1)
                {
                    UnpackEdge(kv.Key, out int i1, out int i2);
                    isBoundary[i1] = true;
                    isBoundary[i2] = true;
                }
            }

            // 1) New positions for existing vertices
            var newPositions = new Vector3[vCount];
            for (int i = 0; i < vCount; i++)
            {
                if (isBoundary[i])
                {
                    if (preserveCreases)
                    {
                        // Crease-preserving: keep original position (pin boundary)
                        newPositions[i] = verts[i];
                    }
                    else
                    {
                        // Boundary mask: 3/4 V + 1/8 (N1 + N2) where N1, N2 are boundary neighbors.
                        Vector3 sum = Vector3.zero;
                        int bCount = 0;
                        foreach (int n in neighbors[i])
                        {
                            if (isBoundary[n])
                            {
                                // Only include neighbors that share a boundary edge with i
                                long key = EdgeKey(i, n);
                                if (edgeAdj.TryGetValue(key, out var adj) && adj.Count == 1)
                                {
                                    sum += verts[n];
                                    bCount++;
                                }
                            }
                        }
                        if (bCount == 2)
                            newPositions[i] = 0.75f * verts[i] + 0.125f * sum;
                        else
                            newPositions[i] = verts[i];
                    }
                }
                else
                {
                    int n = neighbors[i].Count;
                    if (n == 0) { newPositions[i] = verts[i]; continue; }

                    float beta;
                    if (n == 3)
                        beta = 3f / 16f;
                    else
                    {
                        float c = 3f / 8f + Mathf.Cos(2f * Mathf.PI / n) / 4f;
                        beta = (5f / 8f - c * c) / n;
                    }

                    Vector3 sum = Vector3.zero;
                    foreach (int nb in neighbors[i]) sum += verts[nb];

                    newPositions[i] = (1f - n * beta) * verts[i] + beta * sum;
                }
            }

            // 2) Edge midpoints (new vertices)
            int origCount = vCount;
            foreach (var kv in edgeAdj)
            {
                UnpackEdge(kv.Key, out int i1, out int i2);
                var adj = kv.Value;

                Vector3 mid;
                if (adj.Count == 2)
                {
                    // Interior edge: 3/8 (V1+V2) + 1/8 (VL+VR)
                    mid = 0.375f * (verts[i1] + verts[i2])
                        + 0.125f * (verts[adj[0]] + verts[adj[1]]);
                }
                else
                {
                    // Boundary edge: midpoint
                    mid = 0.5f * (verts[i1] + verts[i2]);
                }

                edgeMidpointIdx[kv.Key] = verts.Count;
                verts.Add(mid);
            }

            // Apply new positions to original vertices
            for (int i = 0; i < origCount; i++)
                verts[i] = newPositions[i];

            // 3) Rebuild triangles: each tri (a,b,c) → 4 tris using midpoints (ab, bc, ca).
            var newTris = new List<int>(triCount * 12);
            for (int t = 0; t < triCount; t++)
            {
                int a = tris[t * 3];
                int b = tris[t * 3 + 1];
                int c = tris[t * 3 + 2];

                int mab = edgeMidpointIdx[EdgeKey(a, b)];
                int mbc = edgeMidpointIdx[EdgeKey(b, c)];
                int mca = edgeMidpointIdx[EdgeKey(c, a)];

                newTris.Add(a);   newTris.Add(mab); newTris.Add(mca);
                newTris.Add(b);   newTris.Add(mbc); newTris.Add(mab);
                newTris.Add(c);   newTris.Add(mca); newTris.Add(mbc);
                newTris.Add(mab); newTris.Add(mbc); newTris.Add(mca);
            }

            tris.Clear();
            tris.AddRange(newTris);
        }

        // ═══════════════════════════════════════════════════════════════════
        // sqrt(3) subdivision — barycentric subdivision (simplified).
        // For each triangle, add a center point; connect to the 3 vertices → 3 triangles.
        // Triangle count triples per iteration (not a strict sqrt(3) scheme but a valid
        // barycentric alternate as per story spec).
        // ═══════════════════════════════════════════════════════════════════
        internal static void Sqrt3Subdivide(List<Vector3> verts, List<int> tris, bool preserveCreases)
        {
            int triCount = tris.Count / 3;
            var newTris = new List<int>(triCount * 9);

            for (int t = 0; t < triCount; t++)
            {
                int a = tris[t * 3];
                int b = tris[t * 3 + 1];
                int c = tris[t * 3 + 2];

                Vector3 center = (verts[a] + verts[b] + verts[c]) / 3f;
                int ci = verts.Count;
                verts.Add(center);

                newTris.Add(a); newTris.Add(b); newTris.Add(ci);
                newTris.Add(b); newTris.Add(c); newTris.Add(ci);
                newTris.Add(c); newTris.Add(a); newTris.Add(ci);
            }

            tris.Clear();
            tris.AddRange(newTris);
        }

        // ── Edge helpers ────────────────────────────────────────────────────

        private static long EdgeKey(int i1, int i2)
        {
            int lo = Math.Min(i1, i2);
            int hi = Math.Max(i1, i2);
            return ((long)lo << 32) | (uint)hi;
        }

        private static void UnpackEdge(long key, out int i1, out int i2)
        {
            i1 = (int)(key >> 32);
            i2 = (int)(key & 0xFFFFFFFFL);
        }

        private static void AddEdge(Dictionary<long, List<int>> map, int i1, int i2, int opposite)
        {
            long k = EdgeKey(i1, i2);
            if (!map.TryGetValue(k, out var list))
            {
                list = new List<int>(2);
                map[k] = list;
            }
            list.Add(opposite);
        }
    }
}

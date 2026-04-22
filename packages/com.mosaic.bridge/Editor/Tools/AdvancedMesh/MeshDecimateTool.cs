using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Tools.Shared;

namespace Mosaic.Bridge.Tools.AdvancedMesh
{
    /// <summary>
    /// Mesh decimation via vertex clustering (MVP). Supports single-output decimation and
    /// LOD ladder generation with an optional LODGroup GameObject.
    /// </summary>
    /// <remarks>
    /// The full Quadric Error Metric (Garland &amp; Heckbert 1997) pipeline is scoped for a
    /// follow-up story — see Story 25-2 notes. This implementation uses spatial-grid vertex
    /// clustering which converges toward QEM quality at moderate ratios while being robust,
    /// deterministic, and fast enough to run inline in the Unity editor main thread.
    /// </remarks>
    public static class MeshDecimateTool
    {
        [MosaicTool("mesh/decimate",
                    "Decimates a mesh to a lower triangle count using vertex clustering; can generate a LODGroup ladder",
                    isReadOnly: false, category: "mesh")]
        public static ToolResult<MeshDecimateResult> Execute(MeshDecimateParams p)
        {
            if (p == null)
                return ToolResult<MeshDecimateResult>.Fail("Params required", ErrorCodes.INVALID_PARAM);

            // ---------- Resolve source mesh ----------
            Mesh source = null;
            string sourceName = null;

            if (!string.IsNullOrEmpty(p.SourceMeshPath))
            {
                source = AssetDatabase.LoadAssetAtPath<Mesh>(p.SourceMeshPath);
                if (source == null)
                    return ToolResult<MeshDecimateResult>.Fail(
                        $"Mesh not found at '{p.SourceMeshPath}'", ErrorCodes.NOT_FOUND);
                sourceName = Path.GetFileNameWithoutExtension(p.SourceMeshPath);
            }
            else if (!string.IsNullOrEmpty(p.GameObjectName))
            {
                var go = GameObject.Find(p.GameObjectName);
                if (go == null)
                    return ToolResult<MeshDecimateResult>.Fail(
                        $"GameObject '{p.GameObjectName}' not found", ErrorCodes.NOT_FOUND);
                var mf = go.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null)
                    return ToolResult<MeshDecimateResult>.Fail(
                        $"GameObject '{p.GameObjectName}' has no MeshFilter.sharedMesh",
                        ErrorCodes.INVALID_PARAM);
                source = mf.sharedMesh;
                sourceName = go.name;
            }
            else
            {
                return ToolResult<MeshDecimateResult>.Fail(
                    "Either SourceMeshPath or GameObjectName is required", ErrorCodes.INVALID_PARAM);
            }

            if (source.triangles == null || source.triangles.Length == 0)
                return ToolResult<MeshDecimateResult>.Fail(
                    "Source mesh has no triangles", ErrorCodes.INVALID_PARAM);

            // ---------- Determine LOD ratios ----------
            float[] ratios;
            if (p.LodLevels != null && p.LodLevels.Length > 0)
            {
                ratios = (float[])p.LodLevels.Clone();
            }
            else if (p.GenerateLODGroup)
            {
                ratios = new[] { 1f, 0.5f, 0.25f, 0.1f };
            }
            else
            {
                ratios = new[] { Mathf.Clamp(p.QualityRatio, 0.01f, 1f) };
            }
            for (int i = 0; i < ratios.Length; i++)
                ratios[i] = Mathf.Clamp(ratios[i], 0.01f, 1f);

            // ---------- Prepare output dir ----------
            string savePath = string.IsNullOrEmpty(p.SavePath) ? "Assets/Generated/Mesh/" : p.SavePath;
            if (!savePath.EndsWith("/")) savePath += "/";
            AssetDatabaseHelper.EnsureFolder(savePath.TrimEnd('/'));

            string baseName = string.IsNullOrEmpty(p.OutputName) ? sourceName + "_LOD" : p.OutputName;

            // ---------- Snapshot source data ----------
            var srcVerts = source.vertices;
            var srcNormals = source.normals;
            var srcUVs = source.uv;
            var srcColors = source.colors;
            var srcTris = source.triangles;
            int origTri = srcTris.Length / 3;
            int origVert = srcVerts.Length;

            // Identify boundary vertices (vertices on edges that belong to only one triangle)
            HashSet<int> boundaryVerts = p.PreserveBoundary
                ? ComputeBoundaryVertices(srcTris)
                : null;

            // ---------- Generate each LOD ----------
            var meshPaths = new string[ratios.Length];
            var triCounts = new int[ratios.Length];
            var vertCounts = new int[ratios.Length];
            var generatedMeshes = new Mesh[ratios.Length];

            for (int lod = 0; lod < ratios.Length; lod++)
            {
                Mesh m;
                if (ratios[lod] >= 0.9999f)
                {
                    m = CloneMesh(source);
                }
                else
                {
                    m = VertexClusterDecimate(
                        srcVerts, srcNormals, srcUVs, srcColors, srcTris,
                        ratios[lod], boundaryVerts, p.PreserveUVSeams);
                }

                string assetName = ratios.Length > 1
                    ? $"{baseName}{lod}.asset"
                    : $"{baseName}.asset";
                string assetPath = savePath + assetName;
                m.name = Path.GetFileNameWithoutExtension(assetName);

                // Delete existing if present so the new mesh replaces cleanly
                if (AssetDatabase.LoadAssetAtPath<Mesh>(assetPath) != null)
                    AssetDatabase.DeleteAsset(assetPath);

                AssetDatabase.CreateAsset(m, assetPath);

                meshPaths[lod] = assetPath;
                triCounts[lod] = m.triangles.Length / 3;
                vertCounts[lod] = m.vertexCount;
                generatedMeshes[lod] = m;
            }

            AssetDatabase.SaveAssets();

            // ---------- Optional LODGroup ----------
            string lodGroupName = null;
            if (p.GenerateLODGroup)
            {
                var go = new GameObject(baseName + "_LODGroup");
                var lodGroup = go.AddComponent<LODGroup>();

                var lods = new UnityEngine.LOD[generatedMeshes.Length];
                for (int i = 0; i < generatedMeshes.Length; i++)
                {
                    var child = new GameObject($"LOD{i}");
                    child.transform.SetParent(go.transform, false);
                    var mf = child.AddComponent<MeshFilter>();
                    mf.sharedMesh = generatedMeshes[i];
                    var mr = child.AddComponent<MeshRenderer>();

                    // Simple screen-height thresholds: 0.6, 0.3, 0.15, 0.05, ...
                    float screenH = Mathf.Max(0.01f,
                        0.6f / Mathf.Pow(2f, i));
                    lods[i] = new UnityEngine.LOD(screenH, new Renderer[] { mr });
                }
                lodGroup.SetLODs(lods);
                lodGroup.RecalculateBounds();

                Undo.RegisterCreatedObjectUndo(go, "Create LODGroup");
                lodGroupName = go.name;
            }

            return ToolResult<MeshDecimateResult>.Ok(new MeshDecimateResult
            {
                MeshPaths = meshPaths,
                LodGroupGameObject = lodGroupName,
                OriginalTriangleCount = origTri,
                DecimatedTriangleCounts = triCounts,
                OriginalVertexCount = origVert,
                DecimatedVertexCounts = vertCounts,
                QualityRatios = ratios
            });
        }

        // -------------------------------------------------------------------
        // Vertex-cluster decimation
        // -------------------------------------------------------------------
        static Mesh VertexClusterDecimate(
            Vector3[] verts, Vector3[] normals, Vector2[] uvs, Color[] colors,
            int[] tris, float qualityRatio,
            HashSet<int> preservedVerts, bool preserveUVSeams)
        {
            // Derive grid resolution from desired vertex count.
            int targetVerts = Mathf.Max(8, Mathf.RoundToInt(verts.Length * qualityRatio));
            // cells ≈ targetVerts ⇒ cellsPerAxis = targetVerts^(1/3)
            int cellsPerAxis = Mathf.Max(2, Mathf.CeilToInt(Mathf.Pow(targetVerts, 1f / 3f)));

            // Compute bounds
            Vector3 min = verts[0];
            Vector3 max = verts[0];
            for (int i = 1; i < verts.Length; i++)
            {
                var v = verts[i];
                if (v.x < min.x) min.x = v.x; else if (v.x > max.x) max.x = v.x;
                if (v.y < min.y) min.y = v.y; else if (v.y > max.y) max.y = v.y;
                if (v.z < min.z) min.z = v.z; else if (v.z > max.z) max.z = v.z;
            }
            Vector3 size = max - min;
            if (size.x < 1e-6f) size.x = 1e-6f;
            if (size.y < 1e-6f) size.y = 1e-6f;
            if (size.z < 1e-6f) size.z = 1e-6f;
            Vector3 cellSize = new Vector3(
                size.x / cellsPerAxis, size.y / cellsPerAxis, size.z / cellsPerAxis);

            bool hasNormals = normals != null && normals.Length == verts.Length;
            bool hasUVs = uvs != null && uvs.Length == verts.Length;
            bool hasColors = colors != null && colors.Length == verts.Length;

            // Map source-vertex-index → cluster-index
            var clusterIndex = new int[verts.Length];

            // Cluster key → new vertex index. For preserved (boundary) vertices and optionally
            // UV-seam splits, we use a unique key so those vertices aren't merged.
            var keyToCluster = new Dictionary<long, int>();

            var newVerts = new List<Vector3>();
            var newNormals = hasNormals ? new List<Vector3>() : null;
            var newUVs = hasUVs ? new List<Vector2>() : null;
            var newColors = hasColors ? new List<Color>() : null;
            // Accumulators for averaging
            var accumCount = new List<int>();

            // Unique ID counter for non-clustering vertices (boundary / UV-seam preserved)
            int uniqueCounter = 0;

            for (int i = 0; i < verts.Length; i++)
            {
                long key;
                bool isPreserved = preservedVerts != null && preservedVerts.Contains(i);

                if (isPreserved)
                {
                    // Each preserved vertex becomes its own cluster.
                    key = -1L - uniqueCounter;
                    uniqueCounter++;
                }
                else
                {
                    var v = verts[i];
                    int cx = Mathf.Clamp((int)((v.x - min.x) / cellSize.x), 0, cellsPerAxis - 1);
                    int cy = Mathf.Clamp((int)((v.y - min.y) / cellSize.y), 0, cellsPerAxis - 1);
                    int cz = Mathf.Clamp((int)((v.z - min.z) / cellSize.z), 0, cellsPerAxis - 1);
                    long spatial = ((long)cx * cellsPerAxis + cy) * cellsPerAxis + cz;

                    if (preserveUVSeams && hasUVs)
                    {
                        // Bucket UV coords at low resolution so vertices with distinct UVs
                        // within the same cell aren't merged.
                        int uu = Mathf.RoundToInt(uvs[i].x * 64f);
                        int vv = Mathf.RoundToInt(uvs[i].y * 64f);
                        // Combine into key: spatial occupies lower bits, UV shifted up.
                        key = spatial * 1000003L + (uu * 8192L + vv);
                    }
                    else
                    {
                        key = spatial;
                    }
                }

                if (!keyToCluster.TryGetValue(key, out int ci))
                {
                    ci = newVerts.Count;
                    keyToCluster[key] = ci;
                    newVerts.Add(verts[i]);
                    if (hasNormals) newNormals.Add(normals[i]);
                    if (hasUVs) newUVs.Add(uvs[i]);
                    if (hasColors) newColors.Add(colors[i]);
                    accumCount.Add(1);
                }
                else
                {
                    newVerts[ci] += verts[i];
                    if (hasNormals) newNormals[ci] += normals[i];
                    if (hasUVs) newUVs[ci] += uvs[i];
                    if (hasColors) newColors[ci] += colors[i];
                    accumCount[ci]++;
                }
                clusterIndex[i] = ci;
            }

            // Average accumulated positions/attribs
            for (int i = 0; i < newVerts.Count; i++)
            {
                float inv = 1f / accumCount[i];
                newVerts[i] *= inv;
                if (hasNormals) newNormals[i] = (newNormals[i] * inv).normalized;
                if (hasUVs) newUVs[i] *= inv;
                if (hasColors) newColors[i] = new Color(
                    newColors[i].r * inv, newColors[i].g * inv,
                    newColors[i].b * inv, newColors[i].a * inv);
            }

            // Remap triangles, drop degenerates
            var newTris = new List<int>(tris.Length);
            for (int i = 0; i < tris.Length; i += 3)
            {
                int a = clusterIndex[tris[i]];
                int b = clusterIndex[tris[i + 1]];
                int c = clusterIndex[tris[i + 2]];
                if (a == b || b == c || a == c) continue;
                newTris.Add(a); newTris.Add(b); newTris.Add(c);
            }

            var mesh = new Mesh();
            if (newVerts.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(newVerts);
            if (hasUVs) mesh.SetUVs(0, newUVs);
            if (hasColors) mesh.SetColors(newColors);
            mesh.SetTriangles(newTris, 0);

            if (hasNormals) mesh.SetNormals(newNormals);
            else mesh.RecalculateNormals();

            mesh.RecalculateBounds();
            return mesh;
        }

        // -------------------------------------------------------------------
        // Boundary detection: an edge is a boundary if it appears in exactly one triangle.
        // -------------------------------------------------------------------
        static HashSet<int> ComputeBoundaryVertices(int[] tris)
        {
            var edgeCount = new Dictionary<long, int>();
            for (int i = 0; i < tris.Length; i += 3)
            {
                AddEdge(edgeCount, tris[i], tris[i + 1]);
                AddEdge(edgeCount, tris[i + 1], tris[i + 2]);
                AddEdge(edgeCount, tris[i + 2], tris[i]);
            }
            var boundary = new HashSet<int>();
            foreach (var kv in edgeCount)
            {
                if (kv.Value == 1)
                {
                    // Unpack
                    long k = kv.Key;
                    int b = (int)(k & 0xFFFFFFFF);
                    int a = (int)((k >> 32) & 0xFFFFFFFF);
                    boundary.Add(a);
                    boundary.Add(b);
                }
            }
            return boundary;
        }

        static void AddEdge(Dictionary<long, int> counts, int a, int b)
        {
            int lo = a < b ? a : b;
            int hi = a < b ? b : a;
            long key = ((long)lo << 32) | (uint)hi;
            counts.TryGetValue(key, out int c);
            counts[key] = c + 1;
        }

        // -------------------------------------------------------------------
        // Deep clone a mesh so the returned asset is independent of the source.
        // -------------------------------------------------------------------
        static Mesh CloneMesh(Mesh src)
        {
            var m = new Mesh();
            if (src.vertexCount > 65535)
                m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            m.vertices = src.vertices;
            if (src.normals != null && src.normals.Length > 0) m.normals = src.normals;
            if (src.tangents != null && src.tangents.Length > 0) m.tangents = src.tangents;
            if (src.uv != null && src.uv.Length > 0) m.uv = src.uv;
            if (src.uv2 != null && src.uv2.Length > 0) m.uv2 = src.uv2;
            if (src.colors != null && src.colors.Length > 0) m.colors = src.colors;
            m.triangles = src.triangles;
            m.RecalculateBounds();
            return m;
        }
    }
}

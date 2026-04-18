using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.AdvancedMesh
{
    /// <summary>
    /// Generates a convex hull mesh from a source mesh (asset) or scene GameObject using
    /// a 3D Quickhull algorithm (Barber et al. 1996). Produces a watertight triangle mesh
    /// suitable for physics (convex MeshCollider) or rendering.
    /// </summary>
    public static class MeshConvexHullTool
    {
        [MosaicTool("mesh/convex-hull",
                    "Generates a convex hull mesh from a source mesh or GameObject using Quickhull; optionally attaches a convex MeshCollider",
                    isReadOnly: false, category: "mesh", Context = ToolContext.Both)]
        public static ToolResult<MeshConvexHullResult> Execute(MeshConvexHullParams p)
        {
            if (p == null)
                return ToolResult<MeshConvexHullResult>.Fail("Parameters are required", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrEmpty(p.SourceMeshPath) && string.IsNullOrEmpty(p.GameObjectName))
                return ToolResult<MeshConvexHullResult>.Fail(
                    "Either SourceMeshPath or GameObjectName is required", ErrorCodes.INVALID_PARAM);

            // ── Resolve source mesh ─────────────────────────────────────────
            Mesh sourceMesh = null;
            GameObject sourceGO = null;
            string sourceName = null;

            if (!string.IsNullOrEmpty(p.SourceMeshPath))
            {
                sourceMesh = AssetDatabase.LoadAssetAtPath<Mesh>(p.SourceMeshPath);
                if (sourceMesh == null)
                {
                    // Try loading via any asset (e.g., FBX has a Mesh sub-asset)
                    var all = AssetDatabase.LoadAllAssetsAtPath(p.SourceMeshPath);
                    sourceMesh = all?.OfType<Mesh>().FirstOrDefault();
                }
                if (sourceMesh == null)
                    return ToolResult<MeshConvexHullResult>.Fail(
                        $"Mesh not found at '{p.SourceMeshPath}'", ErrorCodes.NOT_FOUND);
                sourceName = Path.GetFileNameWithoutExtension(p.SourceMeshPath);
            }
            else
            {
                sourceGO = GameObject.Find(p.GameObjectName);
                if (sourceGO == null)
                    return ToolResult<MeshConvexHullResult>.Fail(
                        $"GameObject '{p.GameObjectName}' not found", ErrorCodes.NOT_FOUND);
                var mf = sourceGO.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null)
                    return ToolResult<MeshConvexHullResult>.Fail(
                        $"GameObject '{p.GameObjectName}' has no MeshFilter or mesh", ErrorCodes.INVALID_PARAM);
                sourceMesh = mf.sharedMesh;
                sourceName = sourceGO.name;
            }

            var srcVerts = sourceMesh.vertices;
            if (srcVerts == null || srcVerts.Length < 4)
                return ToolResult<MeshConvexHullResult>.Fail(
                    "Source mesh must have at least 4 vertices for a 3D convex hull",
                    ErrorCodes.INVALID_PARAM);

            int maxVerts = Mathf.Clamp(p.MaxVertices, 4, 255);

            // ── Compute hull ────────────────────────────────────────────────
            List<Vector3> hullVerts;
            List<int> hullTris;
            try
            {
                ComputeConvexHull(srcVerts, out hullVerts, out hullTris);
            }
            catch (Exception ex)
            {
                return ToolResult<MeshConvexHullResult>.Fail(
                    $"Convex hull computation failed: {ex.Message}", ErrorCodes.INTERNAL_ERROR);
            }

            if (hullVerts.Count < 4 || hullTris.Count < 12)
                return ToolResult<MeshConvexHullResult>.Fail(
                    "Degenerate input: hull could not be constructed (points may be coplanar)",
                    ErrorCodes.INVALID_PARAM);

            // ── Simplify if requested ───────────────────────────────────────
            if (p.Simplify && hullVerts.Count > maxVerts)
            {
                SimplifyHull(ref hullVerts, ref hullTris, maxVerts);
            }

            // ── Build Unity mesh ────────────────────────────────────────────
            var mesh = new Mesh { name = ResolveOutputName(p, sourceName) };
            if (hullVerts.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(hullVerts);
            mesh.SetTriangles(hullTris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            string meshPath = null;
            if (p.CreateMesh)
            {
                string saveDir = string.IsNullOrEmpty(p.SavePath) ? "Assets/Generated/Mesh/" : p.SavePath;
                if (!saveDir.EndsWith("/")) saveDir += "/";

                string absoluteDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", saveDir));
                Directory.CreateDirectory(absoluteDir);

                meshPath = saveDir + mesh.name + ".asset";
                AssetDatabase.CreateAsset(mesh, meshPath);
                AssetDatabase.SaveAssets();
                Undo.RegisterCreatedObjectUndo(mesh, "Create Convex Hull Mesh");
            }

            // ── Optional collider ───────────────────────────────────────────
            bool colliderAdded = false;
            string goName = null;
            if (p.CreateCollider && sourceGO != null)
            {
                var existing = sourceGO.GetComponent<MeshCollider>();
                MeshCollider mc = existing != null
                    ? existing
                    : Undo.AddComponent<MeshCollider>(sourceGO);
                Undo.RecordObject(mc, "Set Convex Hull Collider");
                mc.sharedMesh = mesh;
                mc.convex = true;
                colliderAdded = true;
                goName = sourceGO.name;
            }

            return ToolResult<MeshConvexHullResult>.Ok(new MeshConvexHullResult
            {
                MeshPath = meshPath,
                GameObjectName = goName,
                OriginalVertexCount = srcVerts.Length,
                HullVertexCount = hullVerts.Count,
                HullTriangleCount = hullTris.Count / 3,
                ColliderAdded = colliderAdded
            });
        }

        static string ResolveOutputName(MeshConvexHullParams p, string sourceName)
        {
            if (!string.IsNullOrEmpty(p.OutputName)) return p.OutputName;
            return (string.IsNullOrEmpty(sourceName) ? "Mesh" : sourceName) + "_Hull";
        }

        // ────────────────────────────────────────────────────────────────────
        // Quickhull 3D
        // ────────────────────────────────────────────────────────────────────

        const float Epsilon = 1e-6f;

        class Face
        {
            public int A, B, C;              // indices into master vertex list
            public Vector3 Normal;
            public float Offset;             // plane offset: dot(Normal, A)
            public List<int> Outside;        // candidate points outside this face
            public bool Visible;             // flag used during expansion
        }

        /// <summary>
        /// Computes a convex hull over a set of source points, returning a deduplicated
        /// vertex list and counter-clockwise triangle indices (outward-facing normals).
        /// </summary>
        public static void ComputeConvexHull(Vector3[] source, out List<Vector3> outVerts, out List<int> outTris)
        {
            var resultVerts = new List<Vector3>();
            var resultTris = new List<int>();

            // Deduplicate points to improve robustness
            var points = new List<Vector3>();
            var dedup = new HashSet<Vector3>();
            for (int i = 0; i < source.Length; i++)
            {
                var q = Quantize(source[i]);
                if (dedup.Add(q)) points.Add(source[i]);
            }

            if (points.Count < 4)
            {
                outVerts = resultVerts;
                outTris = resultTris;
                return;
            }

            // 1. Initial tetrahedron from extreme points
            if (!BuildInitialTetrahedron(points, out int i0, out int i1, out int i2, out int i3))
            {
                outVerts = resultVerts;
                outTris = resultTris;
                return; // degenerate / coplanar input
            }

            // Master hull-vertex list. We'll add points as they're claimed by faces.
            var hullVertexMap = new Dictionary<int, int>(); // sourceIndex -> hull index
            int HullIndex(int srcIdx)
            {
                if (!hullVertexMap.TryGetValue(srcIdx, out int hi))
                {
                    hi = resultVerts.Count;
                    resultVerts.Add(points[srcIdx]);
                    hullVertexMap[srcIdx] = hi;
                }
                return hi;
            }

            // Create 4 faces of the tetrahedron with outward-facing normals.
            Vector3 centroid = (points[i0] + points[i1] + points[i2] + points[i3]) * 0.25f;
            var faces = new List<Face>
            {
                MakeFace(points, i0, i1, i2, centroid),
                MakeFace(points, i0, i2, i3, centroid),
                MakeFace(points, i0, i3, i1, centroid),
                MakeFace(points, i1, i3, i2, centroid)
            };

            // 2. Assign each remaining point to the first face it sees.
            var initial = new HashSet<int> { i0, i1, i2, i3 };
            for (int p = 0; p < points.Count; p++)
            {
                if (initial.Contains(p)) continue;
                for (int f = 0; f < faces.Count; f++)
                {
                    if (Distance(faces[f], points[p]) > Epsilon)
                    {
                        faces[f].Outside.Add(p);
                        break;
                    }
                }
            }

            // 3. Iteratively expand the hull.
            int safety = 0;
            int maxIter = points.Count * 20 + 1000;
            while (safety++ < maxIter)
            {
                // Find a face with outside points.
                Face work = null;
                for (int f = 0; f < faces.Count; f++)
                {
                    if (faces[f].Outside.Count > 0) { work = faces[f]; break; }
                }
                if (work == null) break;

                // Pick the farthest outside point.
                int eye = work.Outside[0];
                float bestDist = Distance(work, points[eye]);
                for (int k = 1; k < work.Outside.Count; k++)
                {
                    float d = Distance(work, points[work.Outside[k]]);
                    if (d > bestDist) { bestDist = d; eye = work.Outside[k]; }
                }

                // Mark all faces visible from the eye.
                for (int f = 0; f < faces.Count; f++)
                    faces[f].Visible = Distance(faces[f], points[eye]) > Epsilon;

                // Gather horizon edges: edges adjacent to exactly one visible face.
                var edgeCount = new Dictionary<long, int>();
                var edgeDir   = new Dictionary<long, (int a, int b)>(); // oriented from a visible face
                for (int f = 0; f < faces.Count; f++)
                {
                    if (!faces[f].Visible) continue;
                    AddEdge(edgeCount, edgeDir, faces[f].A, faces[f].B);
                    AddEdge(edgeCount, edgeDir, faces[f].B, faces[f].C);
                    AddEdge(edgeCount, edgeDir, faces[f].C, faces[f].A);
                }

                // Orphaned outside points from removed faces.
                var orphans = new List<int>();
                for (int f = faces.Count - 1; f >= 0; f--)
                {
                    if (faces[f].Visible)
                    {
                        orphans.AddRange(faces[f].Outside);
                        faces.RemoveAt(f);
                    }
                }

                // Build new faces from horizon edges to the eye point.
                var newFaces = new List<Face>();
                foreach (var kv in edgeCount)
                {
                    if (kv.Value != 1) continue; // interior edge
                    var (a, b) = edgeDir[kv.Key];
                    var nf = MakeFace(points, a, b, eye, centroid);
                    newFaces.Add(nf);
                }

                if (newFaces.Count == 0) break; // shouldn't happen with valid input

                // Reassign orphan points (excluding eye) to new faces.
                foreach (int op in orphans)
                {
                    if (op == eye) continue;
                    for (int f = 0; f < newFaces.Count; f++)
                    {
                        if (Distance(newFaces[f], points[op]) > Epsilon)
                        {
                            newFaces[f].Outside.Add(op);
                            break;
                        }
                    }
                }

                faces.AddRange(newFaces);
            }

            // 4. Emit deduplicated vertices + triangles.
            for (int f = 0; f < faces.Count; f++)
            {
                int a = HullIndex(faces[f].A);
                int b = HullIndex(faces[f].B);
                int c = HullIndex(faces[f].C);
                resultTris.Add(a); resultTris.Add(b); resultTris.Add(c);
            }

            outVerts = resultVerts;
            outTris = resultTris;
        }

        static Vector3 Quantize(Vector3 v)
        {
            const float q = 1e5f;
            return new Vector3(
                Mathf.Round(v.x * q) / q,
                Mathf.Round(v.y * q) / q,
                Mathf.Round(v.z * q) / q);
        }

        static void AddEdge(Dictionary<long, int> count, Dictionary<long, (int, int)> dir, int a, int b)
        {
            long key = EdgeKey(a, b);
            count.TryGetValue(key, out int c);
            count[key] = c + 1;
            if (c == 0) dir[key] = (a, b); // first occurrence -> oriented from visible face
        }

        static long EdgeKey(int a, int b)
        {
            int lo = Math.Min(a, b);
            int hi = Math.Max(a, b);
            return ((long)lo << 32) | (uint)hi;
        }

        static Face MakeFace(List<Vector3> points, int a, int b, int c, Vector3 interior)
        {
            var pa = points[a];
            var pb = points[b];
            var pc = points[c];
            var n = Vector3.Cross(pb - pa, pc - pa);
            float len = n.magnitude;
            if (len > Epsilon) n /= len;
            // Flip so normal points away from interior point.
            if (Vector3.Dot(n, interior - pa) > 0f)
            {
                (b, c) = (c, b);
                n = -n;
            }
            return new Face
            {
                A = a, B = b, C = c,
                Normal = n,
                Offset = Vector3.Dot(n, points[a]),
                Outside = new List<int>()
            };
        }

        static float Distance(Face f, Vector3 p)
        {
            return Vector3.Dot(f.Normal, p) - f.Offset;
        }

        static bool BuildInitialTetrahedron(List<Vector3> points, out int i0, out int i1, out int i2, out int i3)
        {
            i0 = i1 = i2 = i3 = -1;
            if (points.Count < 4) return false;

            // Extremes along x/y/z
            int minX = 0, maxX = 0, minY = 0, maxY = 0, minZ = 0, maxZ = 0;
            for (int i = 1; i < points.Count; i++)
            {
                var p = points[i];
                if (p.x < points[minX].x) minX = i;
                if (p.x > points[maxX].x) maxX = i;
                if (p.y < points[minY].y) minY = i;
                if (p.y > points[maxY].y) maxY = i;
                if (p.z < points[minZ].z) minZ = i;
                if (p.z > points[maxZ].z) maxZ = i;
            }

            // Pick pair with max spread
            int[] cand = { minX, maxX, minY, maxY, minZ, maxZ };
            float bestD2 = -1f;
            for (int a = 0; a < cand.Length; a++)
            for (int b = a + 1; b < cand.Length; b++)
            {
                float d2 = (points[cand[a]] - points[cand[b]]).sqrMagnitude;
                if (d2 > bestD2) { bestD2 = d2; i0 = cand[a]; i1 = cand[b]; }
            }
            if (bestD2 < Epsilon) return false;

            // Third: farthest from line i0-i1
            Vector3 p0 = points[i0], p1 = points[i1];
            Vector3 dir = (p1 - p0).normalized;
            float bestArea = -1f;
            for (int i = 0; i < points.Count; i++)
            {
                if (i == i0 || i == i1) continue;
                float a2 = Vector3.Cross(points[i] - p0, dir).sqrMagnitude;
                if (a2 > bestArea) { bestArea = a2; i2 = i; }
            }
            if (bestArea < Epsilon) return false;

            // Fourth: farthest from plane (i0,i1,i2)
            Vector3 n = Vector3.Cross(points[i1] - p0, points[i2] - p0).normalized;
            float offset = Vector3.Dot(n, p0);
            float bestAbs = -1f;
            for (int i = 0; i < points.Count; i++)
            {
                if (i == i0 || i == i1 || i == i2) continue;
                float d = Mathf.Abs(Vector3.Dot(n, points[i]) - offset);
                if (d > bestAbs) { bestAbs = d; i3 = i; }
            }
            if (bestAbs < Epsilon) return false;

            return true;
        }

        // ────────────────────────────────────────────────────────────────────
        // Simplification: iteratively remove the vertex whose incident faces
        // contribute the smallest volume, then re-hull the remaining vertices.
        // ────────────────────────────────────────────────────────────────────

        static void SimplifyHull(ref List<Vector3> verts, ref List<int> tris, int maxVerts)
        {
            // Build incidence: for each vertex, sum the area-weighted "volume" of its fan.
            // We use the volume of the tetrahedron (centroid, tri-a, tri-b, tri-c) as the
            // contribution weight; a vertex removed together with its fan loses ~sum of
            // contributions of its incident triangles.
            while (verts.Count > maxVerts)
            {
                Vector3 centroid = Vector3.zero;
                for (int i = 0; i < verts.Count; i++) centroid += verts[i];
                centroid /= verts.Count;

                float[] contrib = new float[verts.Count];
                for (int t = 0; t < tris.Count; t += 3)
                {
                    int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                    float vol = Mathf.Abs(Vector3.Dot(
                        verts[a] - centroid,
                        Vector3.Cross(verts[b] - centroid, verts[c] - centroid))) / 6f;
                    contrib[a] += vol; contrib[b] += vol; contrib[c] += vol;
                }

                // Pick the least-contributing vertex.
                int worst = 0;
                for (int i = 1; i < contrib.Length; i++)
                    if (contrib[i] < contrib[worst]) worst = i;

                // Remove and re-hull.
                var pruned = new Vector3[verts.Count - 1];
                for (int i = 0, j = 0; i < verts.Count; i++)
                    if (i != worst) pruned[j++] = verts[i];

                ComputeConvexHull(pruned, out var newVerts, out var newTris);
                if (newVerts.Count == 0 || newVerts.Count >= verts.Count) break; // no progress
                verts = newVerts;
                tris = newTris;
            }
        }
    }
}

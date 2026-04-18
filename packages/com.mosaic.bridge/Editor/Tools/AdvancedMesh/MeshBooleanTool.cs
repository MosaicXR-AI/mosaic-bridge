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
    /// Constructive Solid Geometry (CSG) boolean operations on Unity meshes
    /// using a BSP tree (Naylor 1990), ported from csg.js (Evan Wallace, public domain).
    /// </summary>
    public static class MeshBooleanTool
    {
        // --------------------------------------------------------------------------------------
        // Public tool entrypoint
        // --------------------------------------------------------------------------------------
        [MosaicTool("mesh/boolean",
                    "Performs CSG boolean operations (union/subtract/intersect) on two meshes using BSP trees.",
                    isReadOnly: false, category: "mesh", Context = ToolContext.Both)]
        public static ToolResult<MeshBooleanResult> Execute(MeshBooleanParams p)
        {
            if (p == null)
                return ToolResult<MeshBooleanResult>.Fail("Params cannot be null", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrWhiteSpace(p.MeshAGameObject))
                return ToolResult<MeshBooleanResult>.Fail("MeshAGameObject is required", ErrorCodes.INVALID_PARAM);
            if (string.IsNullOrWhiteSpace(p.MeshBGameObject))
                return ToolResult<MeshBooleanResult>.Fail("MeshBGameObject is required", ErrorCodes.INVALID_PARAM);
            if (string.IsNullOrWhiteSpace(p.Operation))
                return ToolResult<MeshBooleanResult>.Fail("Operation is required", ErrorCodes.INVALID_PARAM);

            string op = p.Operation.Trim().ToLowerInvariant();
            if (op != "union" && op != "subtract" && op != "intersect")
                return ToolResult<MeshBooleanResult>.Fail(
                    $"Invalid operation '{p.Operation}'. Must be one of: union, subtract, intersect.",
                    ErrorCodes.INVALID_PARAM);

            var goA = GameObject.Find(p.MeshAGameObject);
            if (goA == null)
                return ToolResult<MeshBooleanResult>.Fail(
                    $"GameObject '{p.MeshAGameObject}' not found", ErrorCodes.NOT_FOUND);
            var goB = GameObject.Find(p.MeshBGameObject);
            if (goB == null)
                return ToolResult<MeshBooleanResult>.Fail(
                    $"GameObject '{p.MeshBGameObject}' not found", ErrorCodes.NOT_FOUND);

            var mfA = goA.GetComponent<MeshFilter>();
            var mfB = goB.GetComponent<MeshFilter>();
            if (mfA == null || mfA.sharedMesh == null)
                return ToolResult<MeshBooleanResult>.Fail(
                    $"GameObject '{p.MeshAGameObject}' has no MeshFilter/sharedMesh", ErrorCodes.INVALID_PARAM);
            if (mfB == null || mfB.sharedMesh == null)
                return ToolResult<MeshBooleanResult>.Fail(
                    $"GameObject '{p.MeshBGameObject}' has no MeshFilter/sharedMesh", ErrorCodes.INVALID_PARAM);

            // Convert meshes into world-space polygon lists
            List<Polygon> polysA, polysB;
            try
            {
                polysA = MeshToPolygons(mfA.sharedMesh, goA.transform.localToWorldMatrix);
                polysB = MeshToPolygons(mfB.sharedMesh, goB.transform.localToWorldMatrix);
            }
            catch (Exception ex)
            {
                return ToolResult<MeshBooleanResult>.Fail(
                    $"Failed to convert meshes to polygons: {ex.Message}", ErrorCodes.INTERNAL_ERROR);
            }

            if (polysA.Count == 0 || polysB.Count == 0)
                return ToolResult<MeshBooleanResult>.Fail(
                    "One or both meshes produced zero valid polygons (degenerate triangles).",
                    ErrorCodes.INTERNAL_ERROR);

            // Perform CSG
            List<Polygon> resultPolys;
            try
            {
                resultPolys = PerformCsg(polysA, polysB, op);
            }
            catch (Exception ex)
            {
                return ToolResult<MeshBooleanResult>.Fail(
                    $"CSG operation failed: {ex.Message}", ErrorCodes.INTERNAL_ERROR);
            }

            if (resultPolys == null || resultPolys.Count == 0)
                return ToolResult<MeshBooleanResult>.Fail(
                    "CSG produced no polygons (non-intersecting meshes for subtract/intersect).",
                    ErrorCodes.INTERNAL_ERROR);

            // Build Unity mesh
            var mesh = PolygonsToMesh(resultPolys);

            string outputName = string.IsNullOrWhiteSpace(p.OutputName)
                ? $"{p.MeshAGameObject}_{op}_{p.MeshBGameObject}"
                : p.OutputName;
            mesh.name = outputName;

            // Save asset
            string savePath = string.IsNullOrWhiteSpace(p.SavePath) ? "Assets/Generated/Mesh/" : p.SavePath;
            if (!savePath.EndsWith("/")) savePath += "/";
            string assetPath = savePath + SanitizeFilename(outputName) + ".asset";

            try
            {
                string absoluteDir = Path.GetDirectoryName(
                    Path.GetFullPath(Path.Combine(Application.dataPath, "..", assetPath)));
                if (!string.IsNullOrEmpty(absoluteDir))
                    Directory.CreateDirectory(absoluteDir);

                AssetDatabase.CreateAsset(mesh, assetPath);
                AssetDatabase.SaveAssets();
            }
            catch (Exception ex)
            {
                return ToolResult<MeshBooleanResult>.Fail(
                    $"Failed to save mesh asset: {ex.Message}", ErrorCodes.INTERNAL_ERROR);
            }

            // Create output GameObject
            var outGo = new GameObject(outputName);
            var outMf = outGo.AddComponent<MeshFilter>();
            outMf.sharedMesh = mesh;
            var outMr = outGo.AddComponent<MeshRenderer>();

            // Copy material from A
            var mrA = goA.GetComponent<MeshRenderer>();
            if (mrA != null && mrA.sharedMaterial != null)
                outMr.sharedMaterial = mrA.sharedMaterial;

            if (p.GenerateCollider)
            {
                var mc = outGo.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
            }

            Undo.RegisterCreatedObjectUndo(outGo, "Mesh Boolean");

            // Remove originals if requested
            if (!p.KeepOriginals)
            {
                Undo.DestroyObjectImmediate(goA);
                if (goB != null) Undo.DestroyObjectImmediate(goB);
            }

            return ToolResult<MeshBooleanResult>.Ok(new MeshBooleanResult
            {
                MeshPath = assetPath,
                GameObjectName = outGo.name,
                InstanceId = outGo.GetInstanceID(),
                Operation = op,
                VertexCount = mesh.vertexCount,
                TriangleCount = mesh.triangles.Length / 3,
                OriginalsKept = p.KeepOriginals
            });
        }

        // --------------------------------------------------------------------------------------
        // Mesh <-> Polygon conversion
        // --------------------------------------------------------------------------------------
        private static List<Polygon> MeshToPolygons(Mesh mesh, Matrix4x4 localToWorld)
        {
            var verts = mesh.vertices;
            var tris = mesh.triangles;
            var result = new List<Polygon>(tris.Length / 3);

            for (int i = 0; i < tris.Length; i += 3)
            {
                Vector3 a = localToWorld.MultiplyPoint3x4(verts[tris[i]]);
                Vector3 b = localToWorld.MultiplyPoint3x4(verts[tris[i + 1]]);
                Vector3 c = localToWorld.MultiplyPoint3x4(verts[tris[i + 2]]);

                // Skip degenerate triangles
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-14f) continue;

                result.Add(new Polygon(new List<Vector3> { a, b, c }));
            }
            return result;
        }

        private static Mesh PolygonsToMesh(List<Polygon> polys)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            foreach (var p in polys)
            {
                if (p.Vertices.Count < 3) continue;
                int baseIdx = verts.Count;
                verts.AddRange(p.Vertices);
                // Fan-triangulate
                for (int i = 1; i < p.Vertices.Count - 1; i++)
                {
                    tris.Add(baseIdx);
                    tris.Add(baseIdx + i);
                    tris.Add(baseIdx + i + 1);
                }
            }

            var mesh = new Mesh();
            if (verts.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // --------------------------------------------------------------------------------------
        // CSG driver
        // --------------------------------------------------------------------------------------
        private static List<Polygon> PerformCsg(List<Polygon> a, List<Polygon> b, string op)
        {
            // csg.js algorithm:
            // union(a,b):      a.clipTo(b); b.clipTo(a); b.invert; b.clipTo(a); b.invert; a.build(b.allPolys); return a.allPolys;
            // subtract(a,b):   a.invert; a.clipTo(b); b.clipTo(a); b.invert; b.clipTo(a); b.invert; a.build(b.allPolys); a.invert; return a.allPolys;
            // intersect(a,b):  a.invert; b.clipTo(a); b.invert; a.clipTo(b); b.clipTo(a); a.build(b.allPolys); a.invert; return a.allPolys;
            var A = new Node(ClonePolys(a));
            var B = new Node(ClonePolys(b));

            switch (op)
            {
                case "union":
                    A.ClipTo(B);
                    B.ClipTo(A);
                    B.Invert();
                    B.ClipTo(A);
                    B.Invert();
                    A.Build(B.AllPolygons());
                    return A.AllPolygons();

                case "subtract":
                    A.Invert();
                    A.ClipTo(B);
                    B.ClipTo(A);
                    B.Invert();
                    B.ClipTo(A);
                    B.Invert();
                    A.Build(B.AllPolygons());
                    A.Invert();
                    return A.AllPolygons();

                case "intersect":
                    A.Invert();
                    B.ClipTo(A);
                    B.Invert();
                    A.ClipTo(B);
                    B.ClipTo(A);
                    A.Build(B.AllPolygons());
                    A.Invert();
                    return A.AllPolygons();

                default:
                    throw new ArgumentException("Unknown op");
            }
        }

        private static List<Polygon> ClonePolys(List<Polygon> src)
        {
            var r = new List<Polygon>(src.Count);
            for (int i = 0; i < src.Count; i++) r.Add(src[i].Clone());
            return r;
        }

        private static string SanitizeFilename(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (Array.IndexOf(invalid, chars[i]) >= 0) chars[i] = '_';
            }
            return new string(chars);
        }

        // --------------------------------------------------------------------------------------
        // Polygon (convex, in a single plane)
        // --------------------------------------------------------------------------------------
        private sealed class Polygon
        {
            public List<Vector3> Vertices;
            public Plane Plane;

            public Polygon(List<Vector3> vertices)
            {
                Vertices = vertices;
                Plane = Plane.FromPoints(vertices[0], vertices[1], vertices[2]);
            }

            public Polygon(List<Vector3> vertices, Plane plane)
            {
                Vertices = vertices;
                Plane = plane;
            }

            public Polygon Clone()
            {
                return new Polygon(new List<Vector3>(Vertices), Plane);
            }

            public void Flip()
            {
                Vertices.Reverse();
                Plane = Plane.Flipped();
            }
        }

        // --------------------------------------------------------------------------------------
        // Plane with splitting logic
        // --------------------------------------------------------------------------------------
        private struct Plane
        {
            public Vector3 Normal;
            public float W;
            public const float EPSILON = 1e-5f;

            public static Plane FromPoints(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 n = Vector3.Cross(b - a, c - a).normalized;
                return new Plane { Normal = n, W = Vector3.Dot(n, a) };
            }

            public Plane Flipped()
            {
                return new Plane { Normal = -Normal, W = -W };
            }

            // Split `polygon` by this plane, placing it into one of the four lists.
            public void SplitPolygon(Polygon polygon,
                List<Polygon> coplanarFront, List<Polygon> coplanarBack,
                List<Polygon> front, List<Polygon> back)
            {
                const int COPLANAR = 0, FRONT = 1, BACK = 2, SPANNING = 3;

                int polygonType = 0;
                var types = new int[polygon.Vertices.Count];

                for (int i = 0; i < polygon.Vertices.Count; i++)
                {
                    float t = Vector3.Dot(Normal, polygon.Vertices[i]) - W;
                    int type = (t < -EPSILON) ? BACK : (t > EPSILON) ? FRONT : COPLANAR;
                    polygonType |= type;
                    types[i] = type;
                }

                switch (polygonType)
                {
                    case COPLANAR:
                        if (Vector3.Dot(Normal, polygon.Plane.Normal) > 0)
                            coplanarFront.Add(polygon);
                        else
                            coplanarBack.Add(polygon);
                        break;
                    case FRONT:
                        front.Add(polygon);
                        break;
                    case BACK:
                        back.Add(polygon);
                        break;
                    case SPANNING:
                        var f = new List<Vector3>();
                        var b = new List<Vector3>();
                        int vc = polygon.Vertices.Count;
                        for (int i = 0; i < vc; i++)
                        {
                            int j = (i + 1) % vc;
                            int ti = types[i], tj = types[j];
                            Vector3 vi = polygon.Vertices[i], vj = polygon.Vertices[j];
                            if (ti != BACK) f.Add(vi);
                            if (ti != FRONT) b.Add(vi);
                            if ((ti | tj) == SPANNING)
                            {
                                float t = (W - Vector3.Dot(Normal, vi)) / Vector3.Dot(Normal, vj - vi);
                                Vector3 v = Vector3.Lerp(vi, vj, t);
                                f.Add(v);
                                b.Add(v);
                            }
                        }
                        if (f.Count >= 3) front.Add(new Polygon(f, polygon.Plane));
                        if (b.Count >= 3) back.Add(new Polygon(b, polygon.Plane));
                        break;
                }
            }
        }

        // --------------------------------------------------------------------------------------
        // BSP tree node
        // --------------------------------------------------------------------------------------
        private sealed class Node
        {
            private Plane? _plane;
            private Node _front;
            private Node _back;
            private List<Polygon> _polygons = new List<Polygon>();

            public Node() { }
            public Node(List<Polygon> polygons)
            {
                if (polygons != null && polygons.Count > 0) Build(polygons);
            }

            /// <summary>Flip the solid this node represents (inside-out).</summary>
            public void Invert()
            {
                for (int i = 0; i < _polygons.Count; i++) _polygons[i].Flip();
                if (_plane.HasValue) _plane = _plane.Value.Flipped();
                _front?.Invert();
                _back?.Invert();
                var tmp = _front;
                _front = _back;
                _back = tmp;
            }

            /// <summary>Return all polygons (recursively).</summary>
            public List<Polygon> AllPolygons()
            {
                var result = new List<Polygon>(_polygons);
                if (_front != null) result.AddRange(_front.AllPolygons());
                if (_back != null) result.AddRange(_back.AllPolygons());
                return result;
            }

            /// <summary>Remove polygons inside the given BSP tree.</summary>
            public void ClipTo(Node bsp)
            {
                _polygons = bsp.ClipPolygons(_polygons);
                _front?.ClipTo(bsp);
                _back?.ClipTo(bsp);
            }

            /// <summary>Clip a list of polygons against this BSP (remove ones inside).</summary>
            public List<Polygon> ClipPolygons(List<Polygon> polys)
            {
                if (!_plane.HasValue) return new List<Polygon>(polys);
                var front = new List<Polygon>();
                var back = new List<Polygon>();
                var plane = _plane.Value;
                for (int i = 0; i < polys.Count; i++)
                    plane.SplitPolygon(polys[i], front, back, front, back);

                if (_front != null) front = _front.ClipPolygons(front);
                if (_back != null) back = _back.ClipPolygons(back);
                else back.Clear();

                var result = new List<Polygon>(front.Count + back.Count);
                result.AddRange(front);
                result.AddRange(back);
                return result;
            }

            /// <summary>Build a BSP from a list of polygons. Adds to existing tree.</summary>
            public void Build(List<Polygon> polys)
            {
                if (polys == null || polys.Count == 0) return;
                if (!_plane.HasValue) _plane = polys[0].Plane;

                var front = new List<Polygon>();
                var back = new List<Polygon>();
                var plane = _plane.Value;
                for (int i = 0; i < polys.Count; i++)
                {
                    plane.SplitPolygon(polys[i], _polygons, _polygons, front, back);
                }

                if (front.Count > 0)
                {
                    if (_front == null) _front = new Node();
                    _front.Build(front);
                }
                if (back.Count > 0)
                {
                    if (_back == null) _back = new Node();
                    _back.Build(back);
                }
            }
        }
    }
}

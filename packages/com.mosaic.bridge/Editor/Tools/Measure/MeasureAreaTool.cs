using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Measure
{
    /// <summary>
    /// Computes the area of a polygon (fan triangulation) or of a mesh surface
    /// (sum of triangle areas) and optionally creates a visual in the scene.
    /// </summary>
    public static class MeasureAreaTool
    {
        [MosaicTool("measure/area",
                    "Computes polygon or mesh surface area in m2/cm2/ft2/in2; optionally creates a visual",
                    isReadOnly: false, category: "measure", Context = ToolContext.Both)]
        public static ToolResult<MeasureAreaResult> Execute(MeasureAreaParams p)
        {
            if (p == null)
                return ToolResult<MeasureAreaResult>.Fail("Parameters are required", ErrorCodes.INVALID_PARAM);

            bool hasPolygon = p.Polygon != null && p.Polygon.Length > 0;
            bool hasGO = !string.IsNullOrEmpty(p.GameObjectName);

            if (!hasPolygon && !hasGO)
                return ToolResult<MeasureAreaResult>.Fail(
                    "Either Polygon or GameObjectName is required", ErrorCodes.INVALID_PARAM);

            string unit = string.IsNullOrEmpty(p.Unit) ? "m2" : p.Unit;
            if (!IsValidAreaUnit(unit))
                return ToolResult<MeasureAreaResult>.Fail(
                    $"Invalid unit '{unit}'. Supported: m2, cm2, ft2, in2", ErrorCodes.INVALID_PARAM);

            float areaM2;
            int vertexCount;
            int triangleCount;
            Vector3[] polyVerts = null;

            if (hasPolygon)
            {
                if (p.Polygon.Length < 3)
                    return ToolResult<MeasureAreaResult>.Fail(
                        "Polygon requires at least 3 vertices", ErrorCodes.INVALID_PARAM);

                polyVerts = new Vector3[p.Polygon.Length];
                for (int i = 0; i < p.Polygon.Length; i++)
                {
                    var v = p.Polygon[i];
                    if (v == null || v.Length < 3)
                        return ToolResult<MeasureAreaResult>.Fail(
                            $"Polygon vertex {i} must be [x,y,z]", ErrorCodes.INVALID_PARAM);
                    polyVerts[i] = new Vector3(v[0], v[1], v[2]);
                }

                areaM2 = ComputePolygonArea(polyVerts);
                vertexCount = polyVerts.Length;
                triangleCount = polyVerts.Length - 2;
            }
            else
            {
                var go = GameObject.Find(p.GameObjectName);
                if (go == null)
                    return ToolResult<MeasureAreaResult>.Fail(
                        $"GameObject '{p.GameObjectName}' not found", ErrorCodes.NOT_FOUND);
                var mf = go.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null)
                    return ToolResult<MeasureAreaResult>.Fail(
                        $"GameObject '{p.GameObjectName}' has no MeshFilter or mesh", ErrorCodes.INVALID_PARAM);

                var mesh = mf.sharedMesh;
                var verts = mesh.vertices;
                var tris = mesh.triangles;
                var xform = go.transform;

                // Transform vertices to world space so area reflects scaled mesh.
                var worldVerts = new Vector3[verts.Length];
                for (int i = 0; i < verts.Length; i++)
                    worldVerts[i] = xform.TransformPoint(verts[i]);

                areaM2 = ComputeMeshArea(worldVerts, tris);
                vertexCount = verts.Length;
                triangleCount = tris.Length / 3;
            }

            float converted = ConvertAreaFromM2(areaM2, unit);

            // Optional visual
            if (p.CreateVisual && hasPolygon && polyVerts != null && polyVerts.Length >= 3)
            {
                CreatePolygonVisual(polyVerts, ResolveFillColor(p.FillColor));
            }

            return ToolResult<MeasureAreaResult>.Ok(new MeasureAreaResult
            {
                Area = converted,
                Unit = unit,
                VertexCount = vertexCount,
                TriangleCount = triangleCount
            });
        }

        // ────────────────────────────────────────────────────────────────────
        // Math
        // ────────────────────────────────────────────────────────────────────

        /// <summary>Fan triangulation from vertex 0; sums 0.5 * |cross(AB, AC)|.</summary>
        public static float ComputePolygonArea(Vector3[] verts)
        {
            if (verts == null || verts.Length < 3) return 0f;
            float sum = 0f;
            var v0 = verts[0];
            for (int i = 1; i < verts.Length - 1; i++)
            {
                var ab = verts[i] - v0;
                var ac = verts[i + 1] - v0;
                sum += 0.5f * Vector3.Cross(ab, ac).magnitude;
            }
            return sum;
        }

        /// <summary>Sum of triangle areas over an indexed mesh.</summary>
        public static float ComputeMeshArea(Vector3[] verts, int[] tris)
        {
            if (verts == null || tris == null || tris.Length < 3) return 0f;
            float sum = 0f;
            for (int t = 0; t < tris.Length; t += 3)
            {
                var a = verts[tris[t]];
                var b = verts[tris[t + 1]];
                var c = verts[tris[t + 2]];
                sum += 0.5f * Vector3.Cross(b - a, c - a).magnitude;
            }
            return sum;
        }

        // ────────────────────────────────────────────────────────────────────
        // Units
        // ────────────────────────────────────────────────────────────────────

        internal static bool IsValidAreaUnit(string u)
        {
            return u == "m2" || u == "cm2" || u == "ft2" || u == "in2";
        }

        internal static float ConvertAreaFromM2(float m2, string unit)
        {
            switch (unit)
            {
                case "cm2": return m2 * 10000f;
                case "ft2": return m2 * 10.7639f;
                case "in2": return m2 * 1550f;
                case "m2":
                default:    return m2;
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // Visual
        // ────────────────────────────────────────────────────────────────────

        static Color ResolveFillColor(float[] c)
        {
            if (c == null || c.Length < 4) return new Color(0f, 1f, 0f, 0.3f);
            return new Color(c[0], c[1], c[2], c[3]);
        }

        static void CreatePolygonVisual(Vector3[] verts, Color fill)
        {
            var go = new GameObject("MeasureArea_Visual");
            Undo.RegisterCreatedObjectUndo(go, "Create Measure Area Visual");

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();

            var mesh = new Mesh { name = "MeasureArea_Polygon" };
            mesh.SetVertices(verts);
            var tris = new int[(verts.Length - 2) * 3];
            for (int i = 1, j = 0; i < verts.Length - 1; i++)
            {
                tris[j++] = 0;
                tris[j++] = i;
                tris[j++] = i + 1;
            }
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mf.sharedMesh = mesh;

            var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (shader != null)
            {
                var mat = new Material(shader) { color = fill };
                mr.sharedMaterial = mat;
            }
        }
    }
}

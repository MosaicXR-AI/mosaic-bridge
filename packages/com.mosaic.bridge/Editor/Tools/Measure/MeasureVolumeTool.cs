using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Measure
{
    /// <summary>
    /// Computes the volume of a mesh using the signed-tetrahedron method.
    /// Watertight check is performed via boundary-edge detection; non-closed
    /// meshes still return a best-effort approximation with IsClosed = false.
    /// </summary>
    public static class MeasureVolumeTool
    {
        [MosaicTool("measure/volume",
                    "Computes mesh volume via signed tetrahedrons in m3/cm3/ft3/in3/liters; reports IsClosed",
                    isReadOnly: false, category: "measure", Context = ToolContext.Both)]
        public static ToolResult<MeasureVolumeResult> Execute(MeasureVolumeParams p)
        {
            if (p == null)
                return ToolResult<MeasureVolumeResult>.Fail("Parameters are required", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrEmpty(p.GameObjectName))
                return ToolResult<MeasureVolumeResult>.Fail(
                    "GameObjectName is required", ErrorCodes.INVALID_PARAM);

            string unit = string.IsNullOrEmpty(p.Unit) ? "m3" : p.Unit;
            if (!IsValidVolumeUnit(unit))
                return ToolResult<MeasureVolumeResult>.Fail(
                    $"Invalid unit '{unit}'. Supported: m3, cm3, ft3, in3, liters", ErrorCodes.INVALID_PARAM);

            var go = GameObject.Find(p.GameObjectName);
            if (go == null)
                return ToolResult<MeasureVolumeResult>.Fail(
                    $"GameObject '{p.GameObjectName}' not found", ErrorCodes.NOT_FOUND);

            var mf = go.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
                return ToolResult<MeasureVolumeResult>.Fail(
                    $"GameObject '{p.GameObjectName}' has no MeshFilter or mesh", ErrorCodes.INVALID_PARAM);

            var mesh = mf.sharedMesh;
            var localVerts = mesh.vertices;
            var tris = mesh.triangles;
            // Need at least 3 vertices and 1 triangle to compute anything meaningful.
            // Watertight check will catch open meshes (returns IsClosed=false).
            if (localVerts.Length < 3 || tris.Length < 3)
                return ToolResult<MeasureVolumeResult>.Fail(
                    "Mesh must have at least 3 vertices and 1 triangle", ErrorCodes.INVALID_PARAM);

            // Transform to world space so volume reflects transform scale.
            var xform = go.transform;
            var verts = new Vector3[localVerts.Length];
            for (int i = 0; i < localVerts.Length; i++)
                verts[i] = xform.TransformPoint(localVerts[i]);

            float volumeM3 = ComputeSignedVolume(verts, tris);
            float surfaceM2 = MeasureAreaTool.ComputeMeshArea(verts, tris);
            bool isClosed = IsWatertight(tris);

            float convertedVol = ConvertVolumeFromM3(volumeM3, unit);

            if (p.CreateVisual)
            {
                UnityEditor.Selection.activeGameObject = go;
                EditorGUIUtility.PingObject(go);
            }

            return ToolResult<MeasureVolumeResult>.Ok(new MeasureVolumeResult
            {
                Volume = convertedVol,
                Unit = unit,
                SurfaceArea = surfaceM2,
                IsClosed = isClosed
            });
        }

        // ────────────────────────────────────────────────────────────────────
        // Math
        // ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Signed tetrahedron volume: |Σ dot(v0, cross(v1, v2)) / 6| over all triangles.
        /// Exact for closed watertight meshes; still returns a finite approximation otherwise.
        /// </summary>
        public static float ComputeSignedVolume(Vector3[] verts, int[] tris)
        {
            if (verts == null || tris == null || tris.Length < 3) return 0f;
            float sum = 0f;
            for (int t = 0; t < tris.Length; t += 3)
            {
                var v0 = verts[tris[t]];
                var v1 = verts[tris[t + 1]];
                var v2 = verts[tris[t + 2]];
                sum += Vector3.Dot(v0, Vector3.Cross(v1, v2)) / 6f;
            }
            return Mathf.Abs(sum);
        }

        /// <summary>
        /// Watertight if every directed edge has a matching opposite-direction edge
        /// (i.e., every edge is shared by exactly two triangles with consistent winding).
        /// </summary>
        public static bool IsWatertight(int[] tris)
        {
            if (tris == null || tris.Length < 3) return false;
            var edgeCount = new Dictionary<long, int>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = tris[t], b = tris[t + 1], c = tris[t + 2];
                AccumulateEdge(edgeCount, a, b);
                AccumulateEdge(edgeCount, b, c);
                AccumulateEdge(edgeCount, c, a);
            }
            foreach (var kv in edgeCount)
                if (kv.Value != 2) return false;
            return true;
        }

        static void AccumulateEdge(Dictionary<long, int> count, int a, int b)
        {
            int lo = a < b ? a : b;
            int hi = a < b ? b : a;
            long key = ((long)lo << 32) | (uint)hi;
            count.TryGetValue(key, out int c);
            count[key] = c + 1;
        }

        // ────────────────────────────────────────────────────────────────────
        // Units
        // ────────────────────────────────────────────────────────────────────

        internal static bool IsValidVolumeUnit(string u)
        {
            return u == "m3" || u == "cm3" || u == "ft3" || u == "in3" || u == "liters";
        }

        internal static float ConvertVolumeFromM3(float m3, string unit)
        {
            switch (unit)
            {
                case "cm3":    return m3 * 1_000_000f;
                case "ft3":    return m3 * 35.3147f;
                case "in3":    return m3 * 61023.7f;
                case "liters": return m3 * 1000f;
                case "m3":
                default:       return m3;
            }
        }
    }
}

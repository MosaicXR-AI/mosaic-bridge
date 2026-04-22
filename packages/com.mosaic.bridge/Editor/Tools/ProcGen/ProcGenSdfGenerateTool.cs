using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Tools.Shared;

namespace Mosaic.Bridge.Tools.ProcGen
{
    public static class ProcGenSdfGenerateTool
    {
        [MosaicTool("procgen/sdf-generate",
                    "Generates a signed distance field (Texture3D) from a primitive, mesh, or expression, with optional boolean operations",
                    isReadOnly: false, category: "procgen", Context = ToolContext.Both)]
        public static ToolResult<ProcGenSdfGenerateResult> Execute(ProcGenSdfGenerateParams p)
        {
            if (p == null)
                return ToolResult<ProcGenSdfGenerateResult>.Fail(
                    "Params required", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrEmpty(p.Source))
                return ToolResult<ProcGenSdfGenerateResult>.Fail(
                    "Source is required: 'mesh', 'primitive', or 'expression'", ErrorCodes.INVALID_PARAM);

            string source = p.Source.ToLowerInvariant();
            if (source != "mesh" && source != "primitive" && source != "expression")
                return ToolResult<ProcGenSdfGenerateResult>.Fail(
                    "Source must be 'mesh', 'primitive', or 'expression'", ErrorCodes.INVALID_PARAM);

            if (source == "expression")
                return ToolResult<ProcGenSdfGenerateResult>.Fail(
                    "Expression mode requires compute shader support, coming soon",
                    ErrorCodes.INVALID_PARAM);

            int resolution = Mathf.Clamp(p.Resolution ?? 32, 4, 128);

            Vector3 boundsMin = ToVec3(p.BoundsMin, new Vector3(-5f, -5f, -5f));
            Vector3 boundsMax = ToVec3(p.BoundsMax, new Vector3( 5f,  5f,  5f));
            if (boundsMax.x <= boundsMin.x || boundsMax.y <= boundsMin.y || boundsMax.z <= boundsMin.z)
                return ToolResult<ProcGenSdfGenerateResult>.Fail(
                    "BoundsMax must be greater than BoundsMin on all axes", ErrorCodes.INVALID_PARAM);

            string savePath = string.IsNullOrEmpty(p.SavePath) ? "Assets/Generated/SDF/" : p.SavePath;
            if (!savePath.StartsWith("Assets/"))
                return ToolResult<ProcGenSdfGenerateResult>.Fail(
                    "SavePath must start with 'Assets/'", ErrorCodes.INVALID_PARAM);
            if (!savePath.EndsWith("/")) savePath += "/";

            // --- Primary SDF ---
            float[] sdf;
            string err;
            if (source == "primitive")
            {
                if (string.IsNullOrEmpty(p.PrimitiveType))
                    return ToolResult<ProcGenSdfGenerateResult>.Fail(
                        "PrimitiveType required when Source='primitive'", ErrorCodes.INVALID_PARAM);
                sdf = BuildPrimitiveSdf(p.PrimitiveType, p.PrimitiveSize, resolution, boundsMin, boundsMax, out err);
                if (sdf == null)
                    return ToolResult<ProcGenSdfGenerateResult>.Fail(err, ErrorCodes.INVALID_PARAM);
            }
            else // mesh
            {
                if (string.IsNullOrEmpty(p.MeshPath))
                    return ToolResult<ProcGenSdfGenerateResult>.Fail(
                        "MeshPath required when Source='mesh'", ErrorCodes.INVALID_PARAM);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(p.MeshPath);
                if (mesh == null)
                    return ToolResult<ProcGenSdfGenerateResult>.Fail(
                        $"Mesh not found at '{p.MeshPath}'", ErrorCodes.NOT_FOUND);
                sdf = BuildMeshSdf(mesh, resolution, boundsMin, boundsMax);
            }

            // --- Operation (boolean) ---
            string operation = null;
            if (!string.IsNullOrEmpty(p.Operation))
            {
                operation = p.Operation.ToLowerInvariant();
                if (operation != "union" && operation != "subtract" &&
                    operation != "intersect" && operation != "smooth_union")
                    return ToolResult<ProcGenSdfGenerateResult>.Fail(
                        "Operation must be 'union', 'subtract', 'intersect', or 'smooth_union'",
                        ErrorCodes.INVALID_PARAM);

                bool hasMesh      = !string.IsNullOrEmpty(p.OperandMeshPath);
                bool hasPrimitive = !string.IsNullOrEmpty(p.OperandPrimitive);
                if (!hasMesh && !hasPrimitive)
                    return ToolResult<ProcGenSdfGenerateResult>.Fail(
                        "Operation requires OperandMeshPath or OperandPrimitive", ErrorCodes.INVALID_PARAM);

                float[] operand;
                if (hasMesh)
                {
                    var opMesh = AssetDatabase.LoadAssetAtPath<Mesh>(p.OperandMeshPath);
                    if (opMesh == null)
                        return ToolResult<ProcGenSdfGenerateResult>.Fail(
                            $"Operand mesh not found at '{p.OperandMeshPath}'", ErrorCodes.NOT_FOUND);
                    operand = BuildMeshSdf(opMesh, resolution, boundsMin, boundsMax);
                }
                else
                {
                    operand = BuildPrimitiveSdf(p.OperandPrimitive, p.OperandPrimitiveSize,
                        resolution, boundsMin, boundsMax, out err);
                    if (operand == null)
                        return ToolResult<ProcGenSdfGenerateResult>.Fail(err, ErrorCodes.INVALID_PARAM);
                }

                float blend = Mathf.Max(0.0001f, p.BlendFactor ?? 0.1f);
                ApplyOperation(sdf, operand, operation, blend);
            }

            // --- Write Texture3D ---
            var tex = new Texture3D(resolution, resolution, resolution, TextureFormat.RFloat, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            var colors = new Color[resolution * resolution * resolution];
            float minD = float.MaxValue, maxD = float.MinValue;
            for (int i = 0; i < sdf.Length; i++)
            {
                float v = sdf[i];
                if (v < minD) minD = v;
                if (v > maxD) maxD = v;
                colors[i] = new Color(v, 0f, 0f, 0f);
            }
            tex.SetPixels(colors);
            tex.Apply();

            // Ensure save dir
            AssetDatabaseHelper.EnsureFolder(savePath);
            var fullDir = Path.Combine(Application.dataPath, "..", savePath);

            string name = string.IsNullOrEmpty(p.OutputName)
                ? $"sdf_{source}_{resolution}"
                : p.OutputName;
            string assetPath = savePath + name + ".asset";

            AssetDatabase.CreateAsset(tex, assetPath);
            AssetDatabase.SaveAssets();

            return ToolResult<ProcGenSdfGenerateResult>.Ok(new ProcGenSdfGenerateResult
            {
                AssetPath   = assetPath,
                Resolution  = resolution,
                Source      = source,
                MinDistance = minD,
                MaxDistance = maxD,
                Operation   = operation
            });
        }

        // ── Primitive SDFs ───────────────────────────────────────────────

        private static float[] BuildPrimitiveSdf(string type, float[] size, int res,
            Vector3 bMin, Vector3 bMax, out string err)
        {
            err = null;
            if (string.IsNullOrEmpty(type))
            {
                err = "PrimitiveType required";
                return null;
            }

            string t = type.ToLowerInvariant();
            Vector3 s = ToVec3(size, new Vector3(1f, 1f, 1f));

            Func<Vector3, float> fn;
            switch (t)
            {
                case "sphere":
                    fn = p => p.magnitude - s.x;
                    break;
                case "box":
                {
                    Vector3 he = new Vector3(s.x * 0.5f, s.y * 0.5f, s.z * 0.5f);
                    fn = p =>
                    {
                        Vector3 q = new Vector3(
                            Mathf.Abs(p.x) - he.x,
                            Mathf.Abs(p.y) - he.y,
                            Mathf.Abs(p.z) - he.z);
                        Vector3 qm = new Vector3(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0), Mathf.Max(q.z, 0));
                        return qm.magnitude + Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f);
                    };
                    break;
                }
                case "torus":
                {
                    float R = s.x, r = s.y;
                    fn = p =>
                    {
                        float xz = Mathf.Sqrt(p.x * p.x + p.z * p.z) - R;
                        return Mathf.Sqrt(xz * xz + p.y * p.y) - r;
                    };
                    break;
                }
                case "cylinder":
                {
                    float r = s.x, h = s.y * 0.5f;
                    fn = p =>
                    {
                        float d1 = Mathf.Sqrt(p.x * p.x + p.z * p.z) - r;
                        float d2 = Mathf.Abs(p.y) - h;
                        return Mathf.Max(d1, d2);
                    };
                    break;
                }
                case "cone":
                {
                    // Simple cone: radius r at base, height h, apex at +y
                    float r = s.x, h = s.y;
                    fn = p =>
                    {
                        float q = Mathf.Sqrt(p.x * p.x + p.z * p.z);
                        float k = r / Mathf.Max(h, 0.0001f);
                        float d1 = q - r + k * (p.y + h * 0.5f);
                        float d2 = Mathf.Abs(p.y) - h * 0.5f;
                        return Mathf.Max(d1, d2);
                    };
                    break;
                }
                case "capsule":
                {
                    float r = s.x, h = s.y * 0.5f;
                    fn = p =>
                    {
                        float y = Mathf.Clamp(p.y, -h, h);
                        Vector3 d = new Vector3(p.x, p.y - y, p.z);
                        return d.magnitude - r;
                    };
                    break;
                }
                default:
                    err = $"Unknown PrimitiveType '{type}'. Expected sphere|box|torus|cylinder|cone|capsule";
                    return null;
            }

            return SampleField(fn, res, bMin, bMax);
        }

        // ── Mesh SDF (brute-force per-voxel closest triangle) ────────────

        private static float[] BuildMeshSdf(Mesh mesh, int res, Vector3 bMin, Vector3 bMax)
        {
            var verts = mesh.vertices;
            var tris  = mesh.triangles;
            var bounds = mesh.bounds;

            return SampleField(p =>
            {
                float minD = float.MaxValue;
                for (int i = 0; i < tris.Length; i += 3)
                {
                    Vector3 a = verts[tris[i]];
                    Vector3 b = verts[tris[i + 1]];
                    Vector3 c = verts[tris[i + 2]];
                    float d = PointTriangleDistance(p, a, b, c);
                    if (d < minD) minD = d;
                }
                // Sign via bounds inside/outside test (MVP approximation)
                bool inside = bounds.Contains(p);
                return inside ? -minD : minD;
            }, res, bMin, bMax);
        }

        private static float PointTriangleDistance(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap);
            float d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0f && d2 <= 0f) return (p - a).magnitude;

            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp);
            float d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0f && d4 <= d3) return (p - b).magnitude;

            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0f && d1 >= 0f && d3 <= 0f)
            {
                float v = d1 / (d1 - d3);
                return (p - (a + v * ab)).magnitude;
            }

            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp);
            float d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0f && d5 <= d6) return (p - c).magnitude;

            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0f && d2 >= 0f && d6 <= 0f)
            {
                float w = d2 / (d2 - d6);
                return (p - (a + w * ac)).magnitude;
            }

            float va = d3 * d6 - d5 * d4;
            if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
            {
                float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
                return (p - (b + w * (c - b))).magnitude;
            }

            // Inside triangle - project onto plane
            float denom = 1f / (va + vb + vc);
            float vv = vb * denom;
            float ww = vc * denom;
            return (p - (a + ab * vv + ac * ww)).magnitude;
        }

        // ── Boolean operations ───────────────────────────────────────────

        private static void ApplyOperation(float[] a, float[] b, string op, float blend)
        {
            int n = Mathf.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++)
            {
                float va = a[i];
                float vb = b[i];
                switch (op)
                {
                    case "union":
                        a[i] = Mathf.Min(va, vb);
                        break;
                    case "subtract":
                        a[i] = Mathf.Max(va, -vb);
                        break;
                    case "intersect":
                        a[i] = Mathf.Max(va, vb);
                        break;
                    case "smooth_union":
                    {
                        float h = Mathf.Clamp01(1f - Mathf.Abs(va - vb) / blend);
                        a[i] = Mathf.Min(va, vb) - blend * h;
                        break;
                    }
                }
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────

        private static float[] SampleField(Func<Vector3, float> fn, int res, Vector3 bMin, Vector3 bMax)
        {
            var data = new float[res * res * res];
            Vector3 step = new Vector3(
                (bMax.x - bMin.x) / (res - 1),
                (bMax.y - bMin.y) / (res - 1),
                (bMax.z - bMin.z) / (res - 1));

            for (int z = 0; z < res; z++)
            for (int y = 0; y < res; y++)
            for (int x = 0; x < res; x++)
            {
                Vector3 p = new Vector3(
                    bMin.x + x * step.x,
                    bMin.y + y * step.y,
                    bMin.z + z * step.z);
                data[x + y * res + z * res * res] = fn(p);
            }
            return data;
        }

        private static Vector3 ToVec3(float[] arr, Vector3 fallback)
        {
            if (arr == null || arr.Length < 3) return fallback;
            return new Vector3(arr[0], arr[1], arr[2]);
        }
    }
}

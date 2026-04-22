using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Tools.Shared;

namespace Mosaic.Bridge.Tools.DataViz
{
    /// <summary>
    /// data/heatmap — bakes a scalar-data heatmap onto a target mesh via UV lookup.
    /// Takes scattered (position, value) samples, interpolates them onto the mesh surface
    /// (nearest / linear / IDW), maps through a color gradient LUT, writes a PNG, creates
    /// an Unlit/Texture material, and optionally spawns a legend quad. Story 34-1.
    /// </summary>
    public static class DataHeatmapTool
    {
        static readonly string[] ValidGradients     = { "thermal", "viridis", "jet", "coolwarm", "grayscale" };
        static readonly string[] ValidInterpolation = { "nearest", "linear", "idw" };

        [MosaicTool("data/heatmap",
                    "Bakes a scalar-data heatmap texture onto a target mesh using nearest/linear/IDW interpolation and a color gradient",
                    isReadOnly: false, category: "data", Context = ToolContext.Both)]
        public static ToolResult<DataHeatmapResult> Execute(DataHeatmapParams p)
        {
            p ??= new DataHeatmapParams();

            // ---------- Validation ----------
            if (string.IsNullOrWhiteSpace(p.TargetObject))
                return ToolResult<DataHeatmapResult>.Fail(
                    "TargetObject is required.", ErrorCodes.INVALID_PARAM);

            if (p.DataPoints == null || p.DataPoints.Count == 0)
                return ToolResult<DataHeatmapResult>.Fail(
                    "DataPoints must contain at least one sample.", ErrorCodes.INVALID_PARAM);

            string gradient = string.IsNullOrEmpty(p.ColorGradient)
                ? "thermal"
                : p.ColorGradient.Trim().ToLowerInvariant();
            if (Array.IndexOf(ValidGradients, gradient) < 0)
                return ToolResult<DataHeatmapResult>.Fail(
                    $"Invalid ColorGradient '{p.ColorGradient}'. Valid: thermal, viridis, jet, coolwarm, grayscale.",
                    ErrorCodes.INVALID_PARAM);

            string interp = string.IsNullOrEmpty(p.Interpolation)
                ? "idw"
                : p.Interpolation.Trim().ToLowerInvariant();
            if (Array.IndexOf(ValidInterpolation, interp) < 0)
                return ToolResult<DataHeatmapResult>.Fail(
                    $"Invalid Interpolation '{p.Interpolation}'. Valid: nearest, linear, idw.",
                    ErrorCodes.INVALID_PARAM);

            int resolution = p.Resolution ?? 128;
            if (resolution < 4 || resolution > 4096)
                return ToolResult<DataHeatmapResult>.Fail(
                    $"Resolution must be in [4, 4096]; got {resolution}.", ErrorCodes.OUT_OF_RANGE);

            float idwPower = p.IdwPower ?? 2f;
            if (idwPower <= 0f)
                return ToolResult<DataHeatmapResult>.Fail(
                    "IdwPower must be > 0.", ErrorCodes.INVALID_PARAM);

            bool showLegend = p.ShowLegend ?? false;

            string savePath = string.IsNullOrEmpty(p.SavePath) ? "Assets/Generated/DataViz/" : p.SavePath;
            if (!savePath.StartsWith("Assets/"))
                return ToolResult<DataHeatmapResult>.Fail(
                    "SavePath must start with 'Assets/'.", ErrorCodes.INVALID_PARAM);
            if (!savePath.EndsWith("/")) savePath += "/";

            // Validate data points
            var points = new List<Vector3>(p.DataPoints.Count);
            var values = new List<float>(p.DataPoints.Count);
            for (int i = 0; i < p.DataPoints.Count; i++)
            {
                var dp = p.DataPoints[i];
                if (dp == null || dp.Position == null || dp.Position.Length != 3)
                    return ToolResult<DataHeatmapResult>.Fail(
                        $"DataPoints[{i}].Position must have exactly 3 components.",
                        ErrorCodes.INVALID_PARAM);
                points.Add(new Vector3(dp.Position[0], dp.Position[1], dp.Position[2]));
                values.Add(dp.Value);
            }

            // ---------- Locate target ----------
            var target = GameObject.Find(p.TargetObject);
            if (target == null)
                return ToolResult<DataHeatmapResult>.Fail(
                    $"TargetObject '{p.TargetObject}' not found in scene.", ErrorCodes.NOT_FOUND);

            var meshFilter = target.GetComponent<MeshFilter>();
            if (meshFilter == null || meshFilter.sharedMesh == null)
                return ToolResult<DataHeatmapResult>.Fail(
                    $"TargetObject '{p.TargetObject}' has no MeshFilter/mesh.",
                    ErrorCodes.INVALID_PARAM);

            var mesh = meshFilter.sharedMesh;
            var uvs = mesh.uv;
            var verts = mesh.vertices;
            if (uvs == null || uvs.Length == 0)
                return ToolResult<DataHeatmapResult>.Fail(
                    $"Mesh on '{p.TargetObject}' has no UV0 channel; cannot bake heatmap.",
                    ErrorCodes.INVALID_PARAM);

            // ---------- Value range ----------
            float vmin = p.ValueMin ?? float.PositiveInfinity;
            float vmax = p.ValueMax ?? float.NegativeInfinity;
            if (!p.ValueMin.HasValue || !p.ValueMax.HasValue)
            {
                for (int i = 0; i < values.Count; i++)
                {
                    if (!p.ValueMin.HasValue && values[i] < vmin) vmin = values[i];
                    if (!p.ValueMax.HasValue && values[i] > vmax) vmax = values[i];
                }
                if (!p.ValueMin.HasValue && float.IsPositiveInfinity(vmin)) vmin = 0f;
                if (!p.ValueMax.HasValue && float.IsNegativeInfinity(vmax)) vmax = 1f;
            }
            if (vmax <= vmin) vmax = vmin + 1e-5f;

            // ---------- Build UV->world lookup grid ----------
            // For each texel (u, v) we need the corresponding world-space position on the mesh.
            // Rasterize each triangle into the UV grid using barycentrics.
            var uvWorld = new Vector3[resolution * resolution];
            var uvFilled = new bool[resolution * resolution];

            var localToWorld = target.transform.localToWorldMatrix;
            var tris = mesh.triangles;

            for (int t = 0; t < tris.Length; t += 3)
            {
                int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];
                Vector2 uv0 = uvs[i0], uv1 = uvs[i1], uv2 = uvs[i2];
                Vector3 w0 = localToWorld.MultiplyPoint3x4(verts[i0]);
                Vector3 w1 = localToWorld.MultiplyPoint3x4(verts[i1]);
                Vector3 w2 = localToWorld.MultiplyPoint3x4(verts[i2]);

                float minU = Mathf.Min(uv0.x, Mathf.Min(uv1.x, uv2.x));
                float maxU = Mathf.Max(uv0.x, Mathf.Max(uv1.x, uv2.x));
                float minV = Mathf.Min(uv0.y, Mathf.Min(uv1.y, uv2.y));
                float maxV = Mathf.Max(uv0.y, Mathf.Max(uv1.y, uv2.y));

                int x0 = Mathf.Clamp(Mathf.FloorToInt(minU * resolution), 0, resolution - 1);
                int x1 = Mathf.Clamp(Mathf.CeilToInt (maxU * resolution), 0, resolution - 1);
                int y0 = Mathf.Clamp(Mathf.FloorToInt(minV * resolution), 0, resolution - 1);
                int y1 = Mathf.Clamp(Mathf.CeilToInt (maxV * resolution), 0, resolution - 1);

                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        float u = (x + 0.5f) / resolution;
                        float v = (y + 0.5f) / resolution;
                        if (!BarycentricUV(uv0, uv1, uv2, new Vector2(u, v),
                                            out float b0, out float b1, out float b2))
                            continue;
                        Vector3 world = w0 * b0 + w1 * b1 + w2 * b2;
                        int idx = y * resolution + x;
                        uvWorld[idx] = world;
                        uvFilled[idx] = true;
                    }
                }
            }

            // ---------- Bake ----------
            var colors = new Color[resolution * resolution];
            float invRange = 1f / (vmax - vmin);

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int idx = y * resolution + x;
                    if (!uvFilled[idx])
                    {
                        colors[idx] = new Color(0f, 0f, 0f, 0f); // transparent outside mesh UVs
                        continue;
                    }
                    Vector3 world = uvWorld[idx];
                    float value = Sample(world, points, values, interp, idwPower);
                    float normalized = Mathf.Clamp01((value - vmin) * invRange);
                    colors[idx] = SampleGradient(gradient, normalized);
                }
            }

            // ---------- Write texture asset ----------
            string baseName = string.IsNullOrWhiteSpace(p.Name)
                ? $"Heatmap_{target.name}"
                : p.Name.Trim();
            // Sanitize
            foreach (char c in Path.GetInvalidFileNameChars())
                baseName = baseName.Replace(c, '_');

            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var fullDir = Path.Combine(projectRoot, savePath);
            AssetDatabaseHelper.EnsureFolder(savePath.TrimEnd('/'));

            string texFileName = baseName + ".png";
            string texAssetPath = savePath + texFileName;
            string texFullPath = Path.Combine(fullDir, texFileName);

            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false, false);
            try
            {
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                tex.SetPixels(colors);
                tex.Apply(false, false);
                byte[] png = tex.EncodeToPNG();
                File.WriteAllBytes(texFullPath, png);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
            AssetDatabase.ImportAsset(texAssetPath, ImportAssetOptions.ForceUpdate);
            var loadedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texAssetPath);

            // ---------- Material ----------
            var shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Unlit/Transparent")
                         ?? Shader.Find("Standard");
            var mat = new Material(shader) { name = baseName + "_Mat" };
            if (loadedTex != null)
            {
                if (mat.HasProperty("_MainTex"))  mat.SetTexture("_MainTex", loadedTex);
                if (mat.HasProperty("_BaseMap"))  mat.SetTexture("_BaseMap", loadedTex);
            }

            string matAssetPath = savePath + baseName + "_Mat.mat";
            AssetDatabase.CreateAsset(mat, matAssetPath);
            AssetDatabase.SaveAssets();

            // ---------- Legend ----------
            string legendName = string.Empty;
            if (showLegend)
            {
                legendName = CreateLegend(target, gradient, vmin, vmax, savePath, baseName);
            }

            return ToolResult<DataHeatmapResult>.Ok(new DataHeatmapResult
            {
                MaterialPath     = matAssetPath,
                TexturePath      = texAssetPath,
                TargetObject     = target.name,
                ValueMin         = vmin,
                ValueMax         = vmax,
                LegendGameObject = legendName,
            });
        }

        // ==========================================================
        // Interpolation
        // ==========================================================

        static float Sample(Vector3 world, List<Vector3> points, List<float> values,
                             string interp, float power)
        {
            switch (interp)
            {
                case "nearest": return SampleNearest(world, points, values);
                case "linear":  return SampleLinear (world, points, values);
                case "idw":
                default:        return SampleIdw    (world, points, values, power);
            }
        }

        static float SampleNearest(Vector3 world, List<Vector3> pts, List<float> vals)
        {
            float bestSqr = float.PositiveInfinity;
            int best = 0;
            for (int i = 0; i < pts.Count; i++)
            {
                float d = (pts[i] - world).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = i; }
            }
            return vals[best];
        }

        /// <summary>
        /// Linear blend of the 3 nearest samples using normalized inverse distance weights —
        /// a stand-in for true barycentric interpolation when samples are unstructured.
        /// </summary>
        static float SampleLinear(Vector3 world, List<Vector3> pts, List<float> vals)
        {
            if (pts.Count == 1) return vals[0];

            // Find 3 nearest
            int k = Mathf.Min(3, pts.Count);
            var bestIdx = new int[k];
            var bestDist = new float[k];
            for (int i = 0; i < k; i++) { bestDist[i] = float.PositiveInfinity; bestIdx[i] = 0; }

            for (int i = 0; i < pts.Count; i++)
            {
                float d = (pts[i] - world).magnitude;
                // Insertion into sorted slots
                for (int s = 0; s < k; s++)
                {
                    if (d < bestDist[s])
                    {
                        for (int j = k - 1; j > s; j--)
                        {
                            bestDist[j] = bestDist[j - 1];
                            bestIdx[j]  = bestIdx [j - 1];
                        }
                        bestDist[s] = d;
                        bestIdx[s]  = i;
                        break;
                    }
                }
            }

            // Exact hit shortcut
            if (bestDist[0] < 1e-6f) return vals[bestIdx[0]];

            float wSum = 0f, vSum = 0f;
            for (int s = 0; s < k; s++)
            {
                float w = 1f / bestDist[s];
                wSum += w;
                vSum += w * vals[bestIdx[s]];
            }
            return vSum / wSum;
        }

        static float SampleIdw(Vector3 world, List<Vector3> pts, List<float> vals, float power)
        {
            float wSum = 0f, vSum = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                float d = (pts[i] - world).magnitude;
                if (d < 1e-6f) return vals[i];
                float w = 1f / Mathf.Pow(d, power);
                wSum += w;
                vSum += w * vals[i];
            }
            return wSum > 0f ? vSum / wSum : 0f;
        }

        // ==========================================================
        // UV rasterization helper
        // ==========================================================

        static bool BarycentricUV(Vector2 a, Vector2 b, Vector2 c, Vector2 p,
                                   out float ba, out float bb, out float bc)
        {
            Vector2 v0 = b - a, v1 = c - a, v2 = p - a;
            float d00 = Vector2.Dot(v0, v0);
            float d01 = Vector2.Dot(v0, v1);
            float d11 = Vector2.Dot(v1, v1);
            float d20 = Vector2.Dot(v2, v0);
            float d21 = Vector2.Dot(v2, v1);
            float denom = d00 * d11 - d01 * d01;
            if (Mathf.Abs(denom) < 1e-12f) { ba = bb = bc = 0f; return false; }
            float inv = 1f / denom;
            bb = (d11 * d20 - d01 * d21) * inv;
            bc = (d00 * d21 - d01 * d20) * inv;
            ba = 1f - bb - bc;
            const float eps = -1e-4f;
            return ba >= eps && bb >= eps && bc >= eps;
        }

        // ==========================================================
        // Color gradients
        // ==========================================================

        static Color SampleGradient(string name, float t)
        {
            t = Mathf.Clamp01(t);
            Color[] lut;
            switch (name)
            {
                case "viridis":
                    lut = new[]
                    {
                        new Color(0.267f, 0.005f, 0.329f, 1f), // purple
                        new Color(0.231f, 0.318f, 0.545f, 1f), // blue
                        new Color(0.129f, 0.569f, 0.549f, 1f), // teal
                        new Color(0.365f, 0.788f, 0.384f, 1f), // green
                        new Color(0.992f, 0.906f, 0.145f, 1f), // yellow
                    };
                    break;
                case "jet":
                    lut = new[]
                    {
                        new Color(0f,    0f,    1f, 1f),
                        new Color(0f,    1f,    1f, 1f),
                        new Color(0f,    1f,    0f, 1f),
                        new Color(1f,    1f,    0f, 1f),
                        new Color(1f,    0f,    0f, 1f),
                    };
                    break;
                case "coolwarm":
                    lut = new[]
                    {
                        new Color(0.23f, 0.30f, 0.75f, 1f),
                        new Color(1f,    1f,    1f,    1f),
                        new Color(0.71f, 0.02f, 0.15f, 1f),
                    };
                    break;
                case "grayscale":
                    lut = new[]
                    {
                        new Color(0f, 0f, 0f, 1f),
                        new Color(1f, 1f, 1f, 1f),
                    };
                    break;
                case "thermal":
                default:
                    lut = new[]
                    {
                        new Color(0f,    0f,    0f,    1f), // black
                        new Color(1f,    0f,    0f,    1f), // red
                        new Color(1f,    0.5f,  0f,    1f), // orange
                        new Color(1f,    1f,    0f,    1f), // yellow
                        new Color(1f,    1f,    1f,    1f), // white
                    };
                    break;
            }
            if (lut.Length == 1) return lut[0];
            float scaled = t * (lut.Length - 1);
            int i0 = Mathf.FloorToInt(scaled);
            int i1 = Mathf.Min(i0 + 1, lut.Length - 1);
            float f = scaled - i0;
            return Color.Lerp(lut[i0], lut[i1], f);
        }

        // ==========================================================
        // Legend
        // ==========================================================

        static string CreateLegend(GameObject target, string gradient,
                                    float vmin, float vmax,
                                    string savePath, string baseName)
        {
            const int legendW = 16, legendH = 128;
            var legendColors = new Color[legendW * legendH];
            for (int y = 0; y < legendH; y++)
            {
                float t = (float)y / (legendH - 1);
                Color c = SampleGradient(gradient, t);
                for (int x = 0; x < legendW; x++)
                    legendColors[y * legendW + x] = c;
            }

            var projectRoot = Application.dataPath.Replace("/Assets", "");
            var fullDir = Path.Combine(projectRoot, savePath);
            AssetDatabaseHelper.EnsureFolder(savePath.TrimEnd('/'));

            string legendTexFile = baseName + "_Legend.png";
            string legendTexAsset = savePath + legendTexFile;
            string legendTexFull = Path.Combine(fullDir, legendTexFile);

            var tex = new Texture2D(legendW, legendH, TextureFormat.RGBA32, false, false);
            try
            {
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                tex.SetPixels(legendColors);
                tex.Apply(false, false);
                File.WriteAllBytes(legendTexFull, tex.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
            AssetDatabase.ImportAsset(legendTexAsset, ImportAssetOptions.ForceUpdate);
            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(legendTexAsset);

            var shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
            var legendMat = new Material(shader) { name = baseName + "_LegendMat" };
            if (loaded != null)
            {
                if (legendMat.HasProperty("_MainTex")) legendMat.SetTexture("_MainTex", loaded);
                if (legendMat.HasProperty("_BaseMap")) legendMat.SetTexture("_BaseMap", loaded);
            }
            string legendMatAsset = savePath + baseName + "_LegendMat.mat";
            AssetDatabase.CreateAsset(legendMat, legendMatAsset);
            AssetDatabase.SaveAssets();

            // Create child quad with gradient + two label children (min/max)
            var legendGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
            legendGO.name = $"{target.name}_HeatmapLegend";
            var col = legendGO.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.DestroyImmediate(col);
            legendGO.transform.SetParent(target.transform, worldPositionStays: false);
            legendGO.transform.localPosition = new Vector3(1.2f, 0f, 0f);
            legendGO.transform.localRotation = Quaternion.identity;
            legendGO.transform.localScale    = new Vector3(0.15f, 1f, 1f);
            var mr = legendGO.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = legendMat;

            // Simple label children carry the min/max values in their name for now —
            // full TextMeshPro integration is optional.
            var minLabel = new GameObject($"Min_{vmin:F3}");
            minLabel.transform.SetParent(legendGO.transform, worldPositionStays: false);
            minLabel.transform.localPosition = new Vector3(0f, -0.55f, 0f);

            var maxLabel = new GameObject($"Max_{vmax:F3}");
            maxLabel.transform.SetParent(legendGO.transform, worldPositionStays: false);
            maxLabel.transform.localPosition = new Vector3(0f, 0.55f, 0f);

            return legendGO.name;
        }
    }
}

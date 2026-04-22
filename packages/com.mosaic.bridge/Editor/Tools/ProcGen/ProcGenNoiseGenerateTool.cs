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
    public static class ProcGenNoiseGenerateTool
    {
        private static readonly string[] ValidNoiseTypes   = { "simplex", "perlin", "cellular", "value" };
        private static readonly string[] ValidCombineModes = { "fbm", "ridged", "turbulence", "billow" };
        private static readonly string[] ValidOutputModes  = { "texture", "heightmap", "float_array" };

        [MosaicTool("procgen/noise-generate",
                    "Generates multi-type noise (simplex, perlin, cellular, value) with fractal layering and outputs as texture, heightmap, or float array",
                    isReadOnly: false, category: "procgen")]
        public static ToolResult<ProcGenNoiseGenerateResult> Execute(ProcGenNoiseGenerateParams p)
        {
            // --- Validate & default params ---
            string noiseType = (p.NoiseType ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(noiseType))
                return ToolResult<ProcGenNoiseGenerateResult>.Fail(
                    "NoiseType is required. Valid values: simplex, perlin, cellular, value.",
                    ErrorCodes.INVALID_PARAM);

            if (Array.IndexOf(ValidNoiseTypes, noiseType) < 0)
                return ToolResult<ProcGenNoiseGenerateResult>.Fail(
                    $"Invalid NoiseType '{p.NoiseType}'. Valid values: simplex, perlin, cellular, value.",
                    ErrorCodes.INVALID_PARAM);

            string combineMode = (p.CombineMode ?? "fbm").Trim().ToLowerInvariant();
            if (Array.IndexOf(ValidCombineModes, combineMode) < 0)
                return ToolResult<ProcGenNoiseGenerateResult>.Fail(
                    $"Invalid CombineMode '{p.CombineMode}'. Valid values: fbm, ridged, turbulence, billow.",
                    ErrorCodes.INVALID_PARAM);

            string outputMode = (p.Output ?? "texture").Trim().ToLowerInvariant();
            if (Array.IndexOf(ValidOutputModes, outputMode) < 0)
                return ToolResult<ProcGenNoiseGenerateResult>.Fail(
                    $"Invalid Output '{p.Output}'. Valid values: texture, heightmap, float_array.",
                    ErrorCodes.INVALID_PARAM);

            int   resolution  = p.Resolution ?? 256;
            float frequency   = p.Frequency ?? 1.0f;
            int   octaves     = p.Octaves ?? 4;
            float lacunarity  = p.Lacunarity ?? 2.0f;
            float persistence = p.Persistence ?? 0.5f;
            string savePath   = p.SavePath ?? "Assets/Generated/Noise/";

            if (resolution < 2 || resolution > 4096)
                return ToolResult<ProcGenNoiseGenerateResult>.Fail(
                    $"Resolution must be between 2 and 4096. Got {resolution}.",
                    ErrorCodes.OUT_OF_RANGE);

            if (octaves < 1 || octaves > 16)
                return ToolResult<ProcGenNoiseGenerateResult>.Fail(
                    $"Octaves must be between 1 and 16. Got {octaves}.",
                    ErrorCodes.OUT_OF_RANGE);

            // Deterministic seed
            int seed = p.Seed ?? new System.Random().Next();
            var rng  = new System.Random(seed);
            float offsetX = (float)(rng.NextDouble() * 10000.0);
            float offsetY = (float)(rng.NextDouble() * 10000.0);

            // --- Generate noise ---
            int totalPixels = resolution * resolution;
            float[] noiseData = new float[totalPixels];
            float rawMin = float.MaxValue;
            float rawMax = float.MinValue;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float nx = (float)x / resolution;
                    float ny = (float)y / resolution;

                    float value = EvaluateFractal(noiseType, combineMode, nx, ny,
                                                  frequency, octaves, lacunarity, persistence,
                                                  offsetX, offsetY, rng, seed);

                    if (value < rawMin) rawMin = value;
                    if (value > rawMax) rawMax = value;

                    noiseData[y * resolution + x] = value;
                }
            }

            // Normalize to [0, 1]
            float range = rawMax - rawMin;
            if (range > 0f)
            {
                for (int i = 0; i < totalPixels; i++)
                    noiseData[i] = (noiseData[i] - rawMin) / range;
            }
            else
            {
                for (int i = 0; i < totalPixels; i++)
                    noiseData[i] = 0f;
            }

            // --- Output ---
            string texturePath = null;
            bool heightmapApplied = false;
            string terrainName = null;

            if (outputMode == "texture")
            {
                if (!savePath.StartsWith("Assets/"))
                    return ToolResult<ProcGenNoiseGenerateResult>.Fail(
                        "SavePath must start with 'Assets/'.", ErrorCodes.INVALID_PARAM);

                AssetDatabaseHelper.EnsureFolder(savePath);
                string fullDir = Path.Combine(Application.dataPath, "..", savePath);

                string fileName = $"Noise_{noiseType}_{combineMode}_{seed}.png";
                string assetPath = savePath.TrimEnd('/') + "/" + fileName;
                string fullPath = Path.Combine(fullDir, fileName);

                var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
                try
                {
                    Color[] colors = new Color[totalPixels];
                    for (int i = 0; i < totalPixels; i++)
                    {
                        float v = noiseData[i];
                        colors[i] = new Color(v, v, v, 1f);
                    }
                    tex.SetPixels(colors);
                    tex.Apply();

                    byte[] pngBytes = tex.EncodeToPNG();
                    File.WriteAllBytes(fullPath, pngBytes);
                    AssetDatabase.ImportAsset(assetPath);

                    Undo.RegisterCreatedObjectUndo(
                        AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath),
                        "Mosaic Noise Generate");

                    texturePath = assetPath;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                }
            }

            // Apply to terrain if requested
            if (!string.IsNullOrEmpty(p.ApplyToTerrain))
            {
                var terrainGo = GameObject.Find(p.ApplyToTerrain);
                if (terrainGo == null)
                    return ToolResult<ProcGenNoiseGenerateResult>.Fail(
                        $"Terrain GameObject '{p.ApplyToTerrain}' not found.",
                        ErrorCodes.NOT_FOUND);

                var terrain = terrainGo.GetComponent<Terrain>();
                if (terrain == null)
                    return ToolResult<ProcGenNoiseGenerateResult>.Fail(
                        $"GameObject '{p.ApplyToTerrain}' does not have a Terrain component.",
                        ErrorCodes.INVALID_PARAM);

                var terrainData = terrain.terrainData;
                int heightmapRes = terrainData.heightmapResolution;

                Undo.RecordObject(terrainData, "Mosaic Noise Apply Heightmap");

                float[,] heights = new float[heightmapRes, heightmapRes];
                for (int y = 0; y < heightmapRes; y++)
                {
                    for (int x = 0; x < heightmapRes; x++)
                    {
                        // Resample noise data to terrain resolution
                        float u = (float)x / (heightmapRes - 1);
                        float v = (float)y / (heightmapRes - 1);
                        int sx = Mathf.Clamp(Mathf.FloorToInt(u * (resolution - 1)), 0, resolution - 1);
                        int sy = Mathf.Clamp(Mathf.FloorToInt(v * (resolution - 1)), 0, resolution - 1);
                        heights[y, x] = noiseData[sy * resolution + sx];
                    }
                }

                terrainData.SetHeights(0, 0, heights);
                heightmapApplied = true;
                terrainName = p.ApplyToTerrain;
            }

            return ToolResult<ProcGenNoiseGenerateResult>.Ok(new ProcGenNoiseGenerateResult
            {
                TexturePath      = texturePath,
                Resolution       = resolution,
                NoiseType        = noiseType,
                CombineMode      = combineMode,
                Range            = new NoiseRange { Min = rawMin, Max = rawMax },
                HeightmapApplied = heightmapApplied,
                TerrainName      = terrainName
            });
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Fractal layering
        // ═══════════════════════════════════════════════════════════════════

        private static float EvaluateFractal(string noiseType, string combineMode,
                                              float nx, float ny,
                                              float frequency, int octaves,
                                              float lacunarity, float persistence,
                                              float offsetX, float offsetY,
                                              System.Random rng, int seed)
        {
            float total     = 0f;
            float amplitude = 1f;
            float freq      = frequency;
            float maxVal    = 0f;
            float weight    = 1f; // for ridged

            for (int o = 0; o < octaves; o++)
            {
                float sx = nx * freq + offsetX;
                float sy = ny * freq + offsetY;

                float n = SampleNoise(noiseType, sx, sy, seed);

                switch (combineMode)
                {
                    case "fbm":
                        total += n * amplitude;
                        break;

                    case "ridged":
                        n = 1f - Mathf.Abs(n);
                        n *= n;
                        n *= weight;
                        weight = Mathf.Clamp01(n * 2f);
                        total += n * amplitude;
                        break;

                    case "turbulence":
                        total += Mathf.Abs(n) * amplitude;
                        break;

                    case "billow":
                        total += (2f * Mathf.Abs(n) - 1f) * amplitude;
                        break;
                }

                maxVal += amplitude;
                amplitude *= persistence;
                freq *= lacunarity;
            }

            return total;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Noise primitives
        // ═══════════════════════════════════════════════════════════════════

        private static float SampleNoise(string type, float x, float y, int seed)
        {
            switch (type)
            {
                case "perlin":    return PerlinNoise(x, y);
                case "simplex":   return SimplexNoise(x, y, seed);
                case "cellular":  return CellularNoise(x, y, seed);
                case "value":     return ValueNoise(x, y, seed);
                default:          return 0f;
            }
        }

        // --- Perlin (Unity built-in, returns [0,1], remap to [-1,1]) ---
        private static float PerlinNoise(float x, float y)
        {
            return Mathf.PerlinNoise(x, y) * 2f - 1f;
        }

        // --- OpenSimplex2 (2D, public domain) ---
        private static float SimplexNoise(float x, float y, int seed)
        {
            // Skew
            const float F2 = 0.3660254037844386f;  // (sqrt(3) - 1) / 2
            const float G2 = 0.21132486540518713f; // (3 - sqrt(3)) / 6

            float s = (x + y) * F2;
            int i = FastFloor(x + s);
            int j = FastFloor(y + s);

            float t = (i + j) * G2;
            float x0 = x - (i - t);
            float y0 = y - (j - t);

            int i1, j1;
            if (x0 > y0) { i1 = 1; j1 = 0; }
            else          { i1 = 0; j1 = 1; }

            float x1 = x0 - i1 + G2;
            float y1 = y0 - j1 + G2;
            float x2 = x0 - 1f + 2f * G2;
            float y2 = y0 - 1f + 2f * G2;

            float n0 = 0f, n1 = 0f, n2 = 0f;

            float t0 = 0.5f - x0 * x0 - y0 * y0;
            if (t0 >= 0f)
            {
                t0 *= t0;
                int gi0 = Hash2D(i, j, seed) & 7;
                n0 = t0 * t0 * Grad2(gi0, x0, y0);
            }

            float t1 = 0.5f - x1 * x1 - y1 * y1;
            if (t1 >= 0f)
            {
                t1 *= t1;
                int gi1 = Hash2D(i + i1, j + j1, seed) & 7;
                n1 = t1 * t1 * Grad2(gi1, x1, y1);
            }

            float t2 = 0.5f - x2 * x2 - y2 * y2;
            if (t2 >= 0f)
            {
                t2 *= t2;
                int gi2 = Hash2D(i + 1, j + 1, seed) & 7;
                n2 = t2 * t2 * Grad2(gi2, x2, y2);
            }

            // Scale to roughly [-1, 1]
            return 70f * (n0 + n1 + n2);
        }

        // --- Cellular / Worley noise ---
        private static float CellularNoise(float x, float y, int seed)
        {
            int xi = FastFloor(x);
            int yi = FastFloor(y);
            float fx = x - xi;
            float fy = y - yi;

            float minDist1 = float.MaxValue;
            float minDist2 = float.MaxValue;

            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = xi + dx;
                    int cy = yi + dy;

                    // Deterministic jitter per cell
                    int h = Hash2D(cx, cy, seed);
                    float px = dx + ((h & 0xFF) / 255f) - fx;
                    float py = dy + (((h >> 8) & 0xFF) / 255f) - fy;

                    float dist = px * px + py * py;

                    if (dist < minDist1)
                    {
                        minDist2 = minDist1;
                        minDist1 = dist;
                    }
                    else if (dist < minDist2)
                    {
                        minDist2 = dist;
                    }
                }
            }

            // F2 - F1 gives classic cellular look; remap to [-1, 1]
            float f1 = Mathf.Sqrt(minDist1);
            float f2 = Mathf.Sqrt(minDist2);
            return (f2 - f1) * 2f - 1f;
        }

        // --- Value noise (hash-based with bicubic-style smoothing) ---
        private static float ValueNoise(float x, float y, int seed)
        {
            int xi = FastFloor(x);
            int yi = FastFloor(y);
            float fx = x - xi;
            float fy = y - yi;

            // Smoothstep
            float u = fx * fx * (3f - 2f * fx);
            float v = fy * fy * (3f - 2f * fy);

            float a = HashFloat(xi,     yi,     seed);
            float b = HashFloat(xi + 1, yi,     seed);
            float c = HashFloat(xi,     yi + 1, seed);
            float d = HashFloat(xi + 1, yi + 1, seed);

            float ab = Mathf.Lerp(a, b, u);
            float cd = Mathf.Lerp(c, d, u);

            // Returns [-1, 1]
            return Mathf.Lerp(ab, cd, v) * 2f - 1f;
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Hash & gradient helpers
        // ═══════════════════════════════════════════════════════════════════

        private static int FastFloor(float x)
        {
            int xi = (int)x;
            return x < xi ? xi - 1 : xi;
        }

        /// <summary>Simple integer hash combining coordinates with seed.</summary>
        private static int Hash2D(int x, int y, int seed)
        {
            int h = seed;
            h ^= x * 374761393;
            h ^= y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            return h ^ (h >> 16);
        }

        /// <summary>Hash to float [0, 1].</summary>
        private static float HashFloat(int x, int y, int seed)
        {
            int h = Hash2D(x, y, seed);
            return (h & 0x7FFFFFFF) / (float)int.MaxValue;
        }

        private static readonly float[] Grad2X = { 1, -1, 1, -1, 1, 0, -1, 0 };
        private static readonly float[] Grad2Y = { 1, 1, -1, -1, 0, 1, 0, -1 };

        private static float Grad2(int hash, float x, float y)
        {
            return Grad2X[hash] * x + Grad2Y[hash] * y;
        }
    }
}

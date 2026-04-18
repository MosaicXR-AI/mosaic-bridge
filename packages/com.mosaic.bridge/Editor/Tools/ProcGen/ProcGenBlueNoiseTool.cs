using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.ProcGen
{
    public static class ProcGenBlueNoiseTool
    {
        private static readonly string[] ValidOutputModes = { "texture", "points" };

        [MosaicTool("procgen/blue-noise",
                    "Generates blue noise textures via void-and-cluster algorithm or blue noise point distributions via Mitchell's best candidate",
                    isReadOnly: false, category: "procgen")]
        public static ToolResult<ProcGenBlueNoiseResult> Execute(ProcGenBlueNoiseParams p)
        {
            // --- Defaults & validation ---
            int resolution = p.Resolution ?? 256;
            int channels   = p.Channels ?? 1;
            bool tiling    = p.Tiling ?? true;
            string output  = (p.Output ?? "texture").Trim().ToLowerInvariant();
            string savePath = p.SavePath ?? "Assets/Generated/BlueNoise/";

            if (Array.IndexOf(ValidOutputModes, output) < 0)
                return ToolResult<ProcGenBlueNoiseResult>.Fail(
                    $"Invalid Output '{p.Output}'. Valid values: texture, points.",
                    ErrorCodes.INVALID_PARAM);

            if (output == "texture")
            {
                if (resolution < 2 || resolution > 4096)
                    return ToolResult<ProcGenBlueNoiseResult>.Fail(
                        $"Resolution must be between 2 and 4096. Got {resolution}.",
                        ErrorCodes.OUT_OF_RANGE);

                if (channels < 1 || channels > 4)
                    return ToolResult<ProcGenBlueNoiseResult>.Fail(
                        $"Channels must be between 1 and 4. Got {channels}.",
                        ErrorCodes.OUT_OF_RANGE);

                if (!savePath.StartsWith("Assets/"))
                    return ToolResult<ProcGenBlueNoiseResult>.Fail(
                        "SavePath must start with 'Assets/'.", ErrorCodes.INVALID_PARAM);
            }

            if (output == "points")
            {
                if (!p.PointCount.HasValue || p.PointCount.Value <= 0)
                    return ToolResult<ProcGenBlueNoiseResult>.Fail(
                        "PointCount is required and must be > 0 for points output.",
                        ErrorCodes.INVALID_PARAM);
            }

            int seed = p.Seed ?? new System.Random().Next();
            var rng  = new System.Random(seed);

            // --- Execute ---
            if (output == "texture")
                return GenerateTexture(resolution, channels, tiling, savePath, seed, rng);
            else
                return GeneratePoints(p, rng);
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Texture output — Void-and-Cluster algorithm
        // ═══════════════════════════════════════════════════════════════════

        private static ToolResult<ProcGenBlueNoiseResult> GenerateTexture(
            int resolution, int channels, bool tiling, string savePath, int seed, System.Random rng)
        {
            int totalPixels = resolution * resolution;

            // Generate one blue noise channel at a time
            float[][] channelData = new float[channels][];
            for (int c = 0; c < channels; c++)
            {
                // Use a different sub-seed per channel for independent patterns
                var channelRng = new System.Random(seed ^ (c * 2654435761u).GetHashCode());
                channelData[c] = VoidAndCluster(resolution, tiling, channelRng);
            }

            // --- Save as PNG ---
            string fullDir = Path.Combine(Application.dataPath, "..", savePath);
            Directory.CreateDirectory(fullDir);

            string fileName = $"BlueNoise_{resolution}x{resolution}_ch{channels}_{seed}.png";
            string assetPath = savePath.TrimEnd('/') + "/" + fileName;
            string fullPath = Path.Combine(fullDir, fileName);

            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            try
            {
                Color[] colors = new Color[totalPixels];
                for (int i = 0; i < totalPixels; i++)
                {
                    float r = channels >= 1 ? channelData[0][i] : 0f;
                    float g = channels >= 2 ? channelData[1][i] : 0f;
                    float b = channels >= 3 ? channelData[2][i] : 0f;
                    float a = channels >= 4 ? channelData[3][i] : 1f;
                    colors[i] = new Color(r, g, b, a);
                }
                tex.SetPixels(colors);
                tex.Apply();

                byte[] pngBytes = tex.EncodeToPNG();
                File.WriteAllBytes(fullPath, pngBytes);
                AssetDatabase.ImportAsset(assetPath);

                var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (imported != null)
                    Undo.RegisterCreatedObjectUndo(imported, "Mosaic Blue Noise Generate");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }

            return ToolResult<ProcGenBlueNoiseResult>.Ok(new ProcGenBlueNoiseResult
            {
                TexturePath = assetPath,
                Resolution  = resolution,
                Channels    = channels,
                Tiling      = tiling,
                PointCount  = 0
            });
        }

        // ─────────────────────────────────────────────────────────────────
        //  Void-and-Cluster core
        //
        //  1. Start with a sparse initial binary pattern (white noise seed).
        //  2. Iteratively remove tightest clusters and fill largest voids,
        //     assigning a rank to each pixel.
        //  3. The ranks, normalized to [0,1], form the blue noise texture.
        //
        //  Energy is computed via a Gaussian kernel with toroidal wrapping
        //  when tiling is enabled.
        // ─────────────────────────────────────────────────────────────────

        internal static float[] VoidAndCluster(int resolution, bool tiling, System.Random rng)
        {
            int N = resolution * resolution;

            // Gaussian sigma — roughly 1.5 is a good default for void-and-cluster
            float sigma = 1.5f;
            float sigma2 = 2f * sigma * sigma;

            // Step 1: Create initial binary pattern — seed ~10% of pixels
            bool[] binary = new bool[N];
            int initialCount = Mathf.Max(1, N / 10);
            var initialSet = new List<int>();

            // Shuffle indices and pick first initialCount
            int[] indices = new int[N];
            for (int i = 0; i < N; i++) indices[i] = i;
            for (int i = N - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int tmp = indices[i];
                indices[i] = indices[j];
                indices[j] = tmp;
            }
            for (int i = 0; i < initialCount; i++)
            {
                binary[indices[i]] = true;
                initialSet.Add(indices[i]);
            }

            // Step 2: Compute energy for each pixel from the initial pattern
            float[] energy = new float[N];
            ComputeEnergy(energy, binary, resolution, tiling, sigma2);

            // Step 3: Phase 1 — Remove tightest clusters from initial pattern
            //   For each set pixel, the one with highest energy is the "tightest cluster".
            //   Remove it, assign rank = number of remaining set pixels.
            float[] ranks = new float[N];
            int setCount = initialCount;

            for (int r = initialCount - 1; r >= 0; r--)
            {
                // Find tightest cluster (highest energy among set pixels)
                int tightest = -1;
                float maxEnergy = float.MinValue;
                for (int i = 0; i < N; i++)
                {
                    if (binary[i] && energy[i] > maxEnergy)
                    {
                        maxEnergy = energy[i];
                        tightest = i;
                    }
                }

                binary[tightest] = false;
                ranks[tightest] = r;
                setCount--;

                // Update energy: subtract this pixel's contribution
                UpdateEnergy(energy, tightest, resolution, tiling, sigma2, -1f);
            }

            // Step 4: Phase 2 — Fill largest voids
            //   Among unset pixels, the one with the lowest energy is the "largest void".
            //   Insert it, assign rank = current set count.
            for (int r = initialCount; r < N; r++)
            {
                // Find largest void (lowest energy among unset pixels)
                int largestVoid = -1;
                float minEnergy = float.MaxValue;
                for (int i = 0; i < N; i++)
                {
                    if (!binary[i] && energy[i] < minEnergy)
                    {
                        minEnergy = energy[i];
                        largestVoid = i;
                    }
                }

                binary[largestVoid] = true;
                ranks[largestVoid] = r;
                setCount++;

                // Update energy: add this pixel's contribution
                UpdateEnergy(energy, largestVoid, resolution, tiling, sigma2, +1f);
            }

            // Step 5: Normalize ranks to [0, 1]
            float invN = 1f / (N - 1);
            float[] result = new float[N];
            for (int i = 0; i < N; i++)
                result[i] = ranks[i] * invN;

            return result;
        }

        /// <summary>
        /// Compute full energy array from scratch based on current binary pattern.
        /// Energy at pixel i = sum of Gaussian(distance(i, j)) for all set pixels j.
        /// </summary>
        private static void ComputeEnergy(float[] energy, bool[] binary, int resolution,
                                           bool tiling, float sigma2)
        {
            int N = resolution * resolution;
            Array.Clear(energy, 0, N);

            // Precompute the Gaussian kernel (only need to go up to ~3*sigma cells)
            int kernelRadius = Mathf.CeilToInt(Mathf.Sqrt(sigma2 * 0.5f) * 3f);
            if (tiling) kernelRadius = Mathf.Min(kernelRadius, resolution / 2);

            for (int i = 0; i < N; i++)
            {
                if (!binary[i]) continue;

                int ix = i % resolution;
                int iy = i / resolution;

                for (int dy = -kernelRadius; dy <= kernelRadius; dy++)
                {
                    for (int dx = -kernelRadius; dx <= kernelRadius; dx++)
                    {
                        int tx, ty;
                        if (tiling)
                        {
                            tx = ((ix + dx) % resolution + resolution) % resolution;
                            ty = ((iy + dy) % resolution + resolution) % resolution;
                        }
                        else
                        {
                            tx = ix + dx;
                            ty = iy + dy;
                            if (tx < 0 || tx >= resolution || ty < 0 || ty >= resolution)
                                continue;
                        }

                        float distSq = dx * dx + dy * dy;
                        float g = Mathf.Exp(-distSq / sigma2);
                        energy[ty * resolution + tx] += g;
                    }
                }
            }
        }

        /// <summary>
        /// Incrementally update energy when a single pixel is added (+1) or removed (-1).
        /// </summary>
        private static void UpdateEnergy(float[] energy, int pixelIndex, int resolution,
                                          bool tiling, float sigma2, float sign)
        {
            int ix = pixelIndex % resolution;
            int iy = pixelIndex / resolution;

            int kernelRadius = Mathf.CeilToInt(Mathf.Sqrt(sigma2 * 0.5f) * 3f);
            if (tiling) kernelRadius = Mathf.Min(kernelRadius, resolution / 2);

            for (int dy = -kernelRadius; dy <= kernelRadius; dy++)
            {
                for (int dx = -kernelRadius; dx <= kernelRadius; dx++)
                {
                    int tx, ty;
                    if (tiling)
                    {
                        tx = ((ix + dx) % resolution + resolution) % resolution;
                        ty = ((iy + dy) % resolution + resolution) % resolution;
                    }
                    else
                    {
                        tx = ix + dx;
                        ty = iy + dy;
                        if (tx < 0 || tx >= resolution || ty < 0 || ty >= resolution)
                            continue;
                    }

                    float distSq = dx * dx + dy * dy;
                    float g = Mathf.Exp(-distSq / sigma2);
                    energy[ty * resolution + tx] += sign * g;
                }
            }
        }

        // ═══════════════════════════════════════════════════════════════════
        //  Points output — Mitchell's Best Candidate algorithm
        // ═══════════════════════════════════════════════════════════════════

        private static ToolResult<ProcGenBlueNoiseResult> GeneratePoints(
            ProcGenBlueNoiseParams p, System.Random rng)
        {
            int pointCount = p.PointCount.Value;
            float[] bMin = p.BoundsMin ?? new float[] { 0f, 0f };
            float[] bMax = p.BoundsMax ?? new float[] { 1f, 1f };

            if (bMin.Length < 2)
                return ToolResult<ProcGenBlueNoiseResult>.Fail(
                    "BoundsMin must have at least 2 elements [x, y].", ErrorCodes.INVALID_PARAM);

            if (bMax.Length < 2)
                return ToolResult<ProcGenBlueNoiseResult>.Fail(
                    "BoundsMax must have at least 2 elements [x, y].", ErrorCodes.INVALID_PARAM);

            if (bMin[0] >= bMax[0] || bMin[1] >= bMax[1])
                return ToolResult<ProcGenBlueNoiseResult>.Fail(
                    "BoundsMax must be greater than BoundsMin on all axes.", ErrorCodes.INVALID_PARAM);

            float width  = bMax[0] - bMin[0];
            float height = bMax[1] - bMin[1];

            // Mitchell's Best Candidate: for each new point, generate k candidates
            // and pick the one farthest from all existing points.
            int k = Mathf.Max(1, pointCount); // candidates per point

            var points = new List<float[]>(pointCount);

            // First point is random
            float fx = (float)(rng.NextDouble() * width + bMin[0]);
            float fy = (float)(rng.NextDouble() * height + bMin[1]);
            points.Add(new[] { fx, fy });

            for (int i = 1; i < pointCount; i++)
            {
                float bestX = 0f, bestY = 0f;
                float bestMinDist = -1f;

                // Increase candidates as the set grows (classic Mitchell scaling)
                int candidates = k;

                for (int c = 0; c < candidates; c++)
                {
                    float cx = (float)(rng.NextDouble() * width + bMin[0]);
                    float cy = (float)(rng.NextDouble() * height + bMin[1]);

                    // Find minimum distance to all existing points
                    float minDist = float.MaxValue;
                    for (int j = 0; j < points.Count; j++)
                    {
                        float ddx = cx - points[j][0];
                        float ddy = cy - points[j][1];
                        float distSq = ddx * ddx + ddy * ddy;
                        if (distSq < minDist) minDist = distSq;
                    }

                    if (minDist > bestMinDist)
                    {
                        bestMinDist = minDist;
                        bestX = cx;
                        bestY = cy;
                    }
                }

                points.Add(new[] { bestX, bestY });
            }

            var resultPoints = points.ToArray();

            return ToolResult<ProcGenBlueNoiseResult>.Ok(new ProcGenBlueNoiseResult
            {
                Points     = resultPoints,
                PointCount = resultPoints.Length,
                Resolution = 0,
                Channels   = 0,
                Tiling     = false
            });
        }
    }
}

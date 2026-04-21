using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Terrains
{
    public static class TerrainHeightTool
    {
        [MosaicTool("terrain/height",
                    "Performs height operations on a terrain: set, flatten, raise-lower, smooth, noise, array. Use 'array' with Heights[] + Width + HeightCells for batch writes — the right primitive for procedural terrain (avoids per-cell iteration). Supports DelayLod for multi-call sequences + BlendMode (replace|add|max|min).",
                    isReadOnly: false)]
        public static ToolResult<TerrainHeightResult> Execute(TerrainHeightParams p)
        {
            var terrain = TerrainToolHelpers.ResolveTerrain(p.InstanceId, p.Name, out string error);
            if (terrain == null)
                return ToolResult<TerrainHeightResult>.Fail(error, ErrorCodes.NOT_FOUND);

            var data = terrain.terrainData;
            int res = data.heightmapResolution;

            Undo.RegisterCompleteObjectUndo(data, "Mosaic: Terrain Height");

            string message;

            switch (p.Action?.ToLowerInvariant())
            {
                case "set":
                    message = ApplyBrush(data, res, p, (_, _2) => p.Height);
                    break;

                case "flatten":
                    float flatHeight = p.Height;
                    var allHeights = new float[res, res];
                    for (int y = 0; y < res; y++)
                        for (int x = 0; x < res; x++)
                            allHeights[y, x] = flatHeight;
                    data.SetHeights(0, 0, allHeights);
                    message = $"Flattened entire terrain to height {flatHeight:F3}";
                    break;

                case "raise-lower":
                    message = ApplyBrush(data, res, p,
                        (current, strength) => Mathf.Clamp01(current + strength));
                    break;

                case "smooth":
                    message = ApplySmooth(data, res, p);
                    break;

                case "noise":
                    message = ApplyNoise(data, res, p);
                    break;

                case "array":
                    message = ApplyArray(data, res, p, out var arrayError);
                    if (arrayError != null)
                        return ToolResult<TerrainHeightResult>.Fail(arrayError, ErrorCodes.INVALID_PARAM);
                    break;

                default:
                    return ToolResult<TerrainHeightResult>.Fail(
                        $"Unknown action '{p.Action}'. Valid actions: set, flatten, raise-lower, smooth, noise, array",
                        ErrorCodes.INVALID_PARAM);
            }

            // DelayLod skips immediate collider rebuild. Caller is responsible for
            // a final non-delayed call (or subsequent action that flushes).
            if (!p.DelayLod) terrain.Flush();

            return ToolResult<TerrainHeightResult>.Ok(new TerrainHeightResult
            {
                Action     = p.Action,
                InstanceId = terrain.gameObject.GetInstanceID(),
                Name       = terrain.gameObject.name,
                Message    = message
            });
        }

        private delegate float BrushOp(float current, float strength);

        private static string ApplyBrush(TerrainData data, int res, TerrainHeightParams p, BrushOp op)
        {
            int centerX = Mathf.Clamp(Mathf.RoundToInt(p.X * (res - 1)), 0, res - 1);
            int centerY = Mathf.Clamp(Mathf.RoundToInt(p.Y * (res - 1)), 0, res - 1);
            int radius = Mathf.Max(1, p.Radius);

            int xMin = Mathf.Max(0, centerX - radius);
            int yMin = Mathf.Max(0, centerY - radius);
            int xMax = Mathf.Min(res - 1, centerX + radius);
            int yMax = Mathf.Min(res - 1, centerY + radius);

            int w = xMax - xMin + 1;
            int h = yMax - yMin + 1;

            var heights = data.GetHeights(xMin, yMin, w, h);

            for (int dy = 0; dy < h; dy++)
            {
                for (int dx = 0; dx < w; dx++)
                {
                    float dist = Vector2.Distance(
                        new Vector2(xMin + dx, yMin + dy),
                        new Vector2(centerX, centerY));
                    if (dist > radius) continue;

                    float falloff = 1f - (dist / radius);
                    float strength = p.Strength * falloff;
                    heights[dy, dx] = op(heights[dy, dx], strength);
                }
            }

            data.SetHeights(xMin, yMin, heights);
            return $"Applied {p.Action} at ({p.X:F2}, {p.Y:F2}) with radius {radius}";
        }

        private static string ApplySmooth(TerrainData data, int res, TerrainHeightParams p)
        {
            int centerX = Mathf.Clamp(Mathf.RoundToInt(p.X * (res - 1)), 0, res - 1);
            int centerY = Mathf.Clamp(Mathf.RoundToInt(p.Y * (res - 1)), 0, res - 1);
            int radius = Mathf.Max(1, p.Radius);

            int xMin = Mathf.Max(0, centerX - radius);
            int yMin = Mathf.Max(0, centerY - radius);
            int xMax = Mathf.Min(res - 1, centerX + radius);
            int yMax = Mathf.Min(res - 1, centerY + radius);

            int w = xMax - xMin + 1;
            int h = yMax - yMin + 1;

            var heights = data.GetHeights(xMin, yMin, w, h);
            var smoothed = (float[,])heights.Clone();

            for (int dy = 1; dy < h - 1; dy++)
            {
                for (int dx = 1; dx < w - 1; dx++)
                {
                    float dist = Vector2.Distance(
                        new Vector2(xMin + dx, yMin + dy),
                        new Vector2(centerX, centerY));
                    if (dist > radius) continue;

                    float avg = (heights[dy - 1, dx] + heights[dy + 1, dx] +
                                 heights[dy, dx - 1] + heights[dy, dx + 1] +
                                 heights[dy, dx]) / 5f;
                    float falloff = 1f - (dist / radius);
                    smoothed[dy, dx] = Mathf.Lerp(heights[dy, dx], avg, p.Strength * falloff);
                }
            }

            data.SetHeights(xMin, yMin, smoothed);
            return $"Smoothed terrain at ({p.X:F2}, {p.Y:F2}) with radius {radius}";
        }

        private static string ApplyNoise(TerrainData data, int res, TerrainHeightParams p)
        {
            var heights = data.GetHeights(0, 0, res, res);
            float seed = p.Seed;

            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    float noise = Mathf.PerlinNoise(
                        (x + seed) / (float)res * 10f,
                        (y + seed) / (float)res * 10f);
                    heights[y, x] = Mathf.Clamp01(heights[y, x] + noise * p.Strength);
                }
            }

            data.SetHeights(0, 0, heights);
            return $"Applied Perlin noise with strength {p.Strength:F2} and seed {p.Seed}";
        }

        private static string ApplyArray(TerrainData data, int res, TerrainHeightParams p, out string error)
        {
            error = null;

            if (p.Heights == null || p.Heights.Length == 0)
            {
                error = "Heights array is required for action 'array'.";
                return null;
            }
            if (p.Width <= 0 || p.HeightCells <= 0)
            {
                error = "Width and HeightCells must be > 0 for action 'array'.";
                return null;
            }
            if (p.Heights.Length != p.Width * p.HeightCells)
            {
                error = $"Heights length {p.Heights.Length} does not match Width*HeightCells ({p.Width * p.HeightCells}).";
                return null;
            }
            if (p.ArrayX < 0 || p.ArrayY < 0 ||
                p.ArrayX + p.Width > res || p.ArrayY + p.HeightCells > res)
            {
                error = $"Array region ({p.ArrayX},{p.ArrayY})+({p.Width}x{p.HeightCells}) exceeds heightmap resolution {res}.";
                return null;
            }

            var blend = (p.BlendMode ?? "replace").ToLowerInvariant();
            if (blend != "replace" && blend != "add" && blend != "max" && blend != "min")
            {
                error = $"Unknown BlendMode '{p.BlendMode}'. Valid: replace, add, max, min.";
                return null;
            }

            // Build the 2D array TerrainData expects — [y, x], row-major from Heights.
            var region = new float[p.HeightCells, p.Width];

            if (blend == "replace")
            {
                for (int y = 0; y < p.HeightCells; y++)
                for (int x = 0; x < p.Width; x++)
                    region[y, x] = Mathf.Clamp01(p.Heights[y * p.Width + x]);
            }
            else
            {
                // For add/max/min we need existing heights to blend against.
                var existing = data.GetHeights(p.ArrayX, p.ArrayY, p.Width, p.HeightCells);
                for (int y = 0; y < p.HeightCells; y++)
                for (int x = 0; x < p.Width; x++)
                {
                    float incoming = p.Heights[y * p.Width + x];
                    float current = existing[y, x];
                    float combined = blend switch
                    {
                        "add" => current + incoming,
                        "max" => Mathf.Max(current, incoming),
                        "min" => Mathf.Min(current, incoming),
                        _     => incoming
                    };
                    region[y, x] = Mathf.Clamp01(combined);
                }
            }

            if (p.DelayLod)
                data.SetHeightsDelayLOD(p.ArrayX, p.ArrayY, region);
            else
                data.SetHeights(p.ArrayX, p.ArrayY, region);

            return $"Applied array {p.Width}x{p.HeightCells} at ({p.ArrayX},{p.ArrayY}) blend={blend} delayLod={p.DelayLod}";
        }
    }
}

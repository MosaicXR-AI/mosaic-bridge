using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Terrains
{
    public static class TerrainDetailTool
    {
        [MosaicTool("terrain/detail",
                    "Detail/grass management: add-prototype (RenderMode/UseInstancing/HealthyColor/DryColor/" +
                    "NoiseSpread/AlignToGround), paint, scatter (tunable ScatterCoverage + masked by slope/" +
                    "height/layer), clear, set-resolution, scatter-mode",
                    isReadOnly: false)]
        public static ToolResult<TerrainDetailResult> Execute(TerrainDetailParams p)
        {
            var terrain = TerrainToolHelpers.ResolveTerrain(p.InstanceId, p.Name, out string error);
            if (terrain == null)
                return ToolResult<TerrainDetailResult>.Fail(error, ErrorCodes.NOT_FOUND);

            var data = terrain.terrainData;

            switch (p.Action?.ToLowerInvariant())
            {
                case "add-prototype":
                    return AddPrototype(terrain, data, p);

                case "paint":
                    return PaintDetail(terrain, data, p);

                case "scatter":
                    return ScatterDetail(terrain, data, p);

                case "clear":
                    return ClearDetail(terrain, data, p);

                case "set-resolution":
                    return SetResolution(terrain, data, p);

                case "scatter-mode":
                    return SetScatterMode(terrain, data, p);

                default:
                    return ToolResult<TerrainDetailResult>.Fail(
                        $"Unknown action '{p.Action}'. Valid actions: add-prototype, paint, scatter, clear, set-resolution, scatter-mode",
                        ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<TerrainDetailResult> AddPrototype(
            UnityEngine.Terrain terrain, TerrainData data, TerrainDetailParams p)
        {
            Undo.RegisterCompleteObjectUndo(data, "Mosaic: Terrain Add Detail Prototype");

            var prototype = new DetailPrototype
            {
                minWidth  = p.MinWidth,
                maxWidth  = p.MaxWidth,
                minHeight = p.MinHeight,
                maxHeight = p.MaxHeight,
                usePrototypeMesh = false
            };

            if (!string.IsNullOrEmpty(p.PrefabPath))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p.PrefabPath);
                if (prefab == null)
                    return ToolResult<TerrainDetailResult>.Fail(
                        $"Prefab not found at '{p.PrefabPath}'", ErrorCodes.NOT_FOUND);
                prototype.prototype = prefab;
                prototype.usePrototypeMesh = true;
            }
            else if (!string.IsNullOrEmpty(p.TexturePath))
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(p.TexturePath);
                if (texture == null)
                    return ToolResult<TerrainDetailResult>.Fail(
                        $"Texture not found at '{p.TexturePath}'", ErrorCodes.NOT_FOUND);
                prototype.prototypeTexture = texture;
            }
            else
            {
                return ToolResult<TerrainDetailResult>.Fail(
                    "Either TexturePath or PrefabPath is required for add-prototype",
                    ErrorCodes.INVALID_PARAM);
            }

            if (!string.IsNullOrEmpty(p.RenderMode))
            {
                if (!TryParseRenderMode(p.RenderMode, out var renderMode))
                    return ToolResult<TerrainDetailResult>.Fail(
                        $"Unknown RenderMode '{p.RenderMode}'. Valid: GrassBillboard, VertexLit, Grass", ErrorCodes.INVALID_PARAM);
                prototype.renderMode = renderMode;
            }
            if (p.UseInstancing.HasValue) prototype.useInstancing = p.UseInstancing.Value;
            if (p.HealthyColor != null)
            {
                if (p.HealthyColor.Length < 3)
                    return ToolResult<TerrainDetailResult>.Fail("HealthyColor requires at least [r, g, b]", ErrorCodes.INVALID_PARAM);
                prototype.healthyColor = new Color(p.HealthyColor[0], p.HealthyColor[1], p.HealthyColor[2],
                    p.HealthyColor.Length >= 4 ? p.HealthyColor[3] : 1f);
            }
            if (p.DryColor != null)
            {
                if (p.DryColor.Length < 3)
                    return ToolResult<TerrainDetailResult>.Fail("DryColor requires at least [r, g, b]", ErrorCodes.INVALID_PARAM);
                prototype.dryColor = new Color(p.DryColor[0], p.DryColor[1], p.DryColor[2],
                    p.DryColor.Length >= 4 ? p.DryColor[3] : 1f);
            }
            if (p.NoiseSpread.HasValue) prototype.noiseSpread = p.NoiseSpread.Value;
            if (p.AlignToGround.HasValue) prototype.alignToGround = p.AlignToGround.Value;

            var prototypes = new List<DetailPrototype>(data.detailPrototypes);
            prototypes.Add(prototype);
            data.detailPrototypes = prototypes.ToArray();

            return ToolResult<TerrainDetailResult>.Ok(new TerrainDetailResult
            {
                Action         = "add-prototype",
                InstanceId     = UnityIds.Of(terrain.gameObject),
                Name           = terrain.gameObject.name,
                PrototypeCount = data.detailPrototypes.Length,
                Message        = $"Added detail prototype (index {data.detailPrototypes.Length - 1})"
            });
        }

        private static ToolResult<TerrainDetailResult> PaintDetail(
            UnityEngine.Terrain terrain, TerrainData data, TerrainDetailParams p)
        {
            if (data.detailPrototypes.Length == 0)
                return ToolResult<TerrainDetailResult>.Fail(
                    "Terrain has no detail prototypes. Use add-prototype first.", ErrorCodes.NOT_PERMITTED);

            if (p.PrototypeIndex < 0 || p.PrototypeIndex >= data.detailPrototypes.Length)
                return ToolResult<TerrainDetailResult>.Fail(
                    $"PrototypeIndex {p.PrototypeIndex} out of range (0..{data.detailPrototypes.Length - 1})",
                    ErrorCodes.OUT_OF_RANGE);

            Undo.RegisterCompleteObjectUndo(data, "Mosaic: Terrain Paint Detail");

            int detailRes = data.detailResolution;
            int centerX = Mathf.Clamp(Mathf.RoundToInt(p.X * (detailRes - 1)), 0, detailRes - 1);
            int centerY = Mathf.Clamp(Mathf.RoundToInt(p.Y * (detailRes - 1)), 0, detailRes - 1);
            int radius = Mathf.Max(1, p.Radius);

            int xMin = Mathf.Max(0, centerX - radius);
            int yMin = Mathf.Max(0, centerY - radius);
            int xMax = Mathf.Min(detailRes - 1, centerX + radius);
            int yMax = Mathf.Min(detailRes - 1, centerY + radius);

            int w = xMax - xMin + 1;
            int h = yMax - yMin + 1;

            var layer = data.GetDetailLayer(xMin, yMin, w, h, p.PrototypeIndex);

            for (int dy = 0; dy < h; dy++)
            {
                for (int dx = 0; dx < w; dx++)
                {
                    float dist = Vector2.Distance(
                        new Vector2(xMin + dx, yMin + dy),
                        new Vector2(centerX, centerY));
                    if (dist > radius) continue;

                    layer[dy, dx] = Mathf.Clamp(p.Density, 0, 16);
                }
            }

            data.SetDetailLayer(xMin, yMin, p.PrototypeIndex, layer);
            terrain.Flush();

            return ToolResult<TerrainDetailResult>.Ok(new TerrainDetailResult
            {
                Action         = "paint",
                InstanceId     = UnityIds.Of(terrain.gameObject),
                Name           = terrain.gameObject.name,
                PrototypeCount = data.detailPrototypes.Length,
                Message        = $"Painted detail {p.PrototypeIndex} at ({p.X:F2}, {p.Y:F2}) with radius {radius}"
            });
        }

        private static ToolResult<TerrainDetailResult> ScatterDetail(
            UnityEngine.Terrain terrain, TerrainData data, TerrainDetailParams p)
        {
            if (data.detailPrototypes.Length == 0)
                return ToolResult<TerrainDetailResult>.Fail(
                    "Terrain has no detail prototypes. Use add-prototype first.", ErrorCodes.NOT_PERMITTED);

            if (p.PrototypeIndex < 0 || p.PrototypeIndex >= data.detailPrototypes.Length)
                return ToolResult<TerrainDetailResult>.Fail(
                    $"PrototypeIndex {p.PrototypeIndex} out of range (0..{data.detailPrototypes.Length - 1})",
                    ErrorCodes.OUT_OF_RANGE);

            Undo.RegisterCompleteObjectUndo(data, "Mosaic: Terrain Scatter Detail");

            int detailRes = data.detailResolution;
            var layer = data.GetDetailLayer(0, 0, detailRes, detailRes, p.PrototypeIndex);
            var rng = new System.Random(p.Seed);
            bool masked = p.MinSlope.HasValue || p.MaxSlope.HasValue ||
                          p.MinHeightWorld.HasValue || p.MaxHeightWorld.HasValue || p.RequiredLayerIndex.HasValue;
            int placedCount = 0;

            for (int y = 0; y < detailRes; y++)
            {
                for (int x = 0; x < detailRes; x++)
                {
                    if (rng.NextDouble() >= p.ScatterCoverage) continue;

                    if (masked)
                    {
                        float nx = (float)x / (detailRes - 1);
                        float ny = (float)y / (detailRes - 1);
                        if (!PassesMask(data, nx, ny, p)) continue;
                    }

                    layer[y, x] = Mathf.Clamp(p.Density, 0, 16);
                    placedCount++;
                }
            }

            data.SetDetailLayer(0, 0, p.PrototypeIndex, layer);
            terrain.Flush();

            return ToolResult<TerrainDetailResult>.Ok(new TerrainDetailResult
            {
                Action         = "scatter",
                InstanceId     = UnityIds.Of(terrain.gameObject),
                Name           = terrain.gameObject.name,
                PrototypeCount = data.detailPrototypes.Length,
                PlacedCount    = placedCount,
                Message        = $"Scattered detail {p.PrototypeIndex} across terrain with seed {p.Seed} " +
                                  $"(coverage {p.ScatterCoverage:P0}{(masked ? ", masked" : "")}, placed {placedCount} cells)"
            });
        }

        private static bool PassesMask(TerrainData data, float x, float z, TerrainDetailParams p)
        {
            if (p.MinSlope.HasValue || p.MaxSlope.HasValue)
            {
                var slope = data.GetSteepness(x, z);
                if (p.MinSlope.HasValue && slope < p.MinSlope.Value) return false;
                if (p.MaxSlope.HasValue && slope > p.MaxSlope.Value) return false;
            }
            if (p.MinHeightWorld.HasValue || p.MaxHeightWorld.HasValue)
            {
                var height = data.GetInterpolatedHeight(x, z);
                if (p.MinHeightWorld.HasValue && height < p.MinHeightWorld.Value) return false;
                if (p.MaxHeightWorld.HasValue && height > p.MaxHeightWorld.Value) return false;
            }
            if (p.RequiredLayerIndex.HasValue)
            {
                if (p.RequiredLayerIndex.Value < 0 || p.RequiredLayerIndex.Value >= data.terrainLayers.Length)
                    return false;
                int alphaRes = data.alphamapResolution;
                int ax = Mathf.Clamp(Mathf.RoundToInt(x * (alphaRes - 1)), 0, alphaRes - 1);
                int az = Mathf.Clamp(Mathf.RoundToInt(z * (alphaRes - 1)), 0, alphaRes - 1);
                var alphas = data.GetAlphamaps(ax, az, 1, 1);
                if (alphas[0, 0, p.RequiredLayerIndex.Value] < p.MinLayerWeight) return false;
            }
            return true;
        }

        private static ToolResult<TerrainDetailResult> SetResolution(
            UnityEngine.Terrain terrain, TerrainData data, TerrainDetailParams p)
        {
            if (p.DetailResolution <= 0)
                return ToolResult<TerrainDetailResult>.Fail("DetailResolution must be > 0", ErrorCodes.INVALID_PARAM);
            if (p.ResolutionPerPatch <= 0)
                return ToolResult<TerrainDetailResult>.Fail("ResolutionPerPatch must be > 0", ErrorCodes.INVALID_PARAM);

            Undo.RegisterCompleteObjectUndo(data, "Mosaic: Terrain Set Detail Resolution");
            data.SetDetailResolution(p.DetailResolution, p.ResolutionPerPatch);
            terrain.Flush();

            return ToolResult<TerrainDetailResult>.Ok(new TerrainDetailResult
            {
                Action             = "set-resolution",
                InstanceId         = UnityIds.Of(terrain.gameObject),
                Name               = terrain.gameObject.name,
                PrototypeCount     = data.detailPrototypes.Length,
                DetailResolution   = data.detailResolution,
                ResolutionPerPatch = data.detailResolutionPerPatch,
                Message            = $"Set detail resolution to {p.DetailResolution} ({p.ResolutionPerPatch} per patch)"
            });
        }

        private static ToolResult<TerrainDetailResult> SetScatterMode(
            UnityEngine.Terrain terrain, TerrainData data, TerrainDetailParams p)
        {
            if (!TryParseScatterMode(p.ScatterMode, out var scatterMode))
                return ToolResult<TerrainDetailResult>.Fail(
                    $"Unknown ScatterMode '{p.ScatterMode}'. Valid: CoverageMode, InstanceCountMode", ErrorCodes.INVALID_PARAM);

            Undo.RegisterCompleteObjectUndo(data, "Mosaic: Terrain Set Detail Scatter Mode");
            data.SetDetailScatterMode(scatterMode);
            terrain.Flush();

            return ToolResult<TerrainDetailResult>.Ok(new TerrainDetailResult
            {
                Action         = "scatter-mode",
                InstanceId     = UnityIds.Of(terrain.gameObject),
                Name           = terrain.gameObject.name,
                PrototypeCount = data.detailPrototypes.Length,
                ScatterMode    = scatterMode.ToString(),
                Message        = $"Set detail scatter mode to {scatterMode}"
            });
        }

        private static bool TryParseRenderMode(string value, out DetailRenderMode result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "grassbillboard": result = DetailRenderMode.GrassBillboard; return true;
                case "vertexlit":      result = DetailRenderMode.VertexLit;      return true;
                case "grass":          result = DetailRenderMode.Grass;          return true;
                default:               result = DetailRenderMode.VertexLit;      return false;
            }
        }

        private static bool TryParseScatterMode(string value, out DetailScatterMode result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "coveragemode":      result = DetailScatterMode.CoverageMode;      return true;
                case "instancecountmode": result = DetailScatterMode.InstanceCountMode; return true;
                default:                  result = DetailScatterMode.CoverageMode;      return false;
            }
        }

        private static ToolResult<TerrainDetailResult> ClearDetail(
            UnityEngine.Terrain terrain, TerrainData data, TerrainDetailParams p)
        {
            Undo.RegisterCompleteObjectUndo(data, "Mosaic: Terrain Clear Detail");

            int detailRes = data.detailResolution;

            // Clear all detail layers or just one
            int startLayer = 0;
            int endLayer = data.detailPrototypes.Length;

            if (p.PrototypeIndex >= 0 && p.PrototypeIndex < data.detailPrototypes.Length)
            {
                startLayer = p.PrototypeIndex;
                endLayer = p.PrototypeIndex + 1;
            }

            for (int l = startLayer; l < endLayer; l++)
            {
                var emptyLayer = new int[detailRes, detailRes];
                data.SetDetailLayer(0, 0, l, emptyLayer);
            }

            terrain.Flush();

            return ToolResult<TerrainDetailResult>.Ok(new TerrainDetailResult
            {
                Action         = "clear",
                InstanceId     = UnityIds.Of(terrain.gameObject),
                Name           = terrain.gameObject.name,
                PrototypeCount = data.detailPrototypes.Length,
                Message        = startLayer + 1 == endLayer
                    ? $"Cleared detail layer {startLayer}"
                    : $"Cleared all {endLayer} detail layers"
            });
        }
    }
}

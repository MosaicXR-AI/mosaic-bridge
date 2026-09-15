using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Terrains
{
    public static class TerrainPaintTool
    {
        [MosaicTool("terrain/paint",
                    "Splatmap painting: add-layer (also MaskMapPath, Metallic, Smoothness, " +
                    "TileOffset, NormalScale, Diffuse RemapMin/Max — full PBR layer authoring, " +
                    "applied whether the .terrainlayer is newly created or an existing one is " +
                    "reused), remove-layer, paint-layer, fill-layer, array (Weights flat " +
                    "[HeightCells*Width] over the rectangle at ArrayX/ArrayY — batch/procedural " +
                    "texturing without brush-call storms), auto (paints LayerIndex wherever " +
                    "Min/MaxSlope degrees and/or Min/MaxHeight match, across the whole terrain).",
                    isReadOnly: false)]
        public static ToolResult<TerrainPaintResult> Execute(TerrainPaintParams p)
        {
            var terrain = TerrainToolHelpers.ResolveTerrain(p.InstanceId, p.Name, out string error);
            if (terrain == null)
                return ToolResult<TerrainPaintResult>.Fail(error, ErrorCodes.NOT_FOUND);

            var data = terrain.terrainData;
            Undo.RegisterCompleteObjectUndo(data, "Mosaic: Terrain Paint");

            switch (p.Action?.ToLowerInvariant())
            {
                case "add-layer":
                    return AddLayer(terrain, data, p);

                case "remove-layer":
                    return RemoveLayer(terrain, data, p);

                case "paint-layer":
                    return PaintLayer(terrain, data, p);

                case "fill-layer":
                    return FillLayer(terrain, data, p);

                case "array":
                    return ArrayLayer(terrain, data, p);

                case "auto":
                    return AutoLayer(terrain, data, p);

                default:
                    return ToolResult<TerrainPaintResult>.Fail(
                        $"Unknown action '{p.Action}'. Valid actions: add-layer, remove-layer, paint-layer, fill-layer, array, auto",
                        ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<TerrainPaintResult> AddLayer(
            UnityEngine.Terrain terrain, TerrainData data, TerrainPaintParams p)
        {
            if (string.IsNullOrEmpty(p.TexturePath))
                return ToolResult<TerrainPaintResult>.Fail(
                    "TexturePath is required for add-layer", ErrorCodes.INVALID_PARAM);

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(p.TexturePath);
            if (texture == null)
                return ToolResult<TerrainPaintResult>.Fail(
                    $"Texture not found at '{p.TexturePath}'", ErrorCodes.NOT_FOUND);

            Texture2D normalMap = null;
            if (!string.IsNullOrEmpty(p.NormalMapPath))
            {
                normalMap = AssetDatabase.LoadAssetAtPath<Texture2D>(p.NormalMapPath);
                if (normalMap == null)
                    return ToolResult<TerrainPaintResult>.Fail(
                        $"Normal map not found at '{p.NormalMapPath}'", ErrorCodes.NOT_FOUND);
            }

            // L14: this used to mint a brand-new .terrainlayer asset on every call, named after
            // the CALLING terrain — so a course's terrain/grid (many adjacent Terrain tiles)
            // ended up with one duplicate layer asset per tile for what should be a single shared
            // layer, and painting one tile never matched its neighbors. The default path is now
            // derived from the texture itself (not the terrain), so repeated add-layer calls with
            // the same TexturePath across different terrains converge on ONE asset, reused rather
            // than recreated.
            string layerPath = !string.IsNullOrEmpty(p.LayerAssetPath)
                ? p.LayerAssetPath
                : $"Assets/TerrainData/{texture.name}.terrainlayer";

            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
            bool reused = layer != null;
            if (!reused)
            {
                layer = new TerrainLayer
                {
                    diffuseTexture = texture,
                    normalMapTexture = normalMap,
                    tileSize = p.TileSize != null && p.TileSize.Length == 2
                        ? new Vector2(p.TileSize[0], p.TileSize[1])
                        : new Vector2(15f, 15f)
                };

                string dir = System.IO.Path.GetDirectoryName(layerPath);
                if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
                    System.IO.Directory.CreateDirectory(
                        System.IO.Path.Combine(Application.dataPath, "..", dir));
                AssetDatabase.CreateAsset(layer, layerPath);
            }

            // Full PBR authoring — applied whether the layer is new or reused, so a second
            // add-layer call can tune an already-shared layer without recreating it.
            if (!string.IsNullOrEmpty(p.MaskMapPath))
            {
                var maskMap = AssetDatabase.LoadAssetAtPath<Texture2D>(p.MaskMapPath);
                if (maskMap == null)
                    return ToolResult<TerrainPaintResult>.Fail(
                        $"Mask map not found at '{p.MaskMapPath}'", ErrorCodes.NOT_FOUND);
                layer.maskMapTexture = maskMap;
            }
            if (p.Metallic.HasValue) layer.metallic = p.Metallic.Value;
            if (p.Smoothness.HasValue) layer.smoothness = p.Smoothness.Value;
            if (p.NormalScale.HasValue) layer.normalScale = p.NormalScale.Value;
            if (p.TileOffset != null)
            {
                if (p.TileOffset.Length != 2)
                    return ToolResult<TerrainPaintResult>.Fail(
                        "TileOffset requires exactly [x, y]", ErrorCodes.INVALID_PARAM);
                layer.tileOffset = new Vector2(p.TileOffset[0], p.TileOffset[1]);
            }
            if (p.DiffuseRemapMin != null)
            {
                if (p.DiffuseRemapMin.Length != 4)
                    return ToolResult<TerrainPaintResult>.Fail(
                        "DiffuseRemapMin requires exactly [r, g, b, a]", ErrorCodes.INVALID_PARAM);
                layer.diffuseRemapMin = new Vector4(p.DiffuseRemapMin[0], p.DiffuseRemapMin[1], p.DiffuseRemapMin[2], p.DiffuseRemapMin[3]);
            }
            if (p.DiffuseRemapMax != null)
            {
                if (p.DiffuseRemapMax.Length != 4)
                    return ToolResult<TerrainPaintResult>.Fail(
                        "DiffuseRemapMax requires exactly [r, g, b, a]", ErrorCodes.INVALID_PARAM);
                layer.diffuseRemapMax = new Vector4(p.DiffuseRemapMax[0], p.DiffuseRemapMax[1], p.DiffuseRemapMax[2], p.DiffuseRemapMax[3]);
            }
            EditorUtility.SetDirty(layer);

            int existingIndex = System.Array.IndexOf(data.terrainLayers, layer);
            int layerIndex;
            string message;
            if (existingIndex >= 0)
            {
                // Already on this specific terrain — idempotent, not a duplicate add.
                layerIndex = existingIndex;
                message = $"Terrain already has layer '{layerPath}' at index {existingIndex} (no change)";
            }
            else
            {
                var layers = new List<TerrainLayer>(data.terrainLayers);
                layers.Add(layer);
                data.terrainLayers = layers.ToArray();
                layerIndex = data.terrainLayers.Length - 1;
                message = reused
                    ? $"Attached existing terrain layer '{layerPath}' (index {layerIndex})"
                    : $"Added terrain layer from '{p.TexturePath}' (index {layerIndex})";
            }

            return ToolResult<TerrainPaintResult>.Ok(new TerrainPaintResult
            {
                Action     = "add-layer",
                InstanceId = UnityIds.Of(terrain.gameObject),
                Name       = terrain.gameObject.name,
                LayerCount = data.terrainLayers.Length,
                LayerIndex = layerIndex,
                Message    = message
            });
        }

        private static ToolResult<TerrainPaintResult> RemoveLayer(
            UnityEngine.Terrain terrain, TerrainData data, TerrainPaintParams p)
        {
            if (p.LayerIndex < 0 || p.LayerIndex >= data.terrainLayers.Length)
                return ToolResult<TerrainPaintResult>.Fail(
                    $"LayerIndex {p.LayerIndex} out of range (0..{data.terrainLayers.Length - 1})",
                    ErrorCodes.OUT_OF_RANGE);

            var layers = new List<TerrainLayer>(data.terrainLayers);
            layers.RemoveAt(p.LayerIndex);
            data.terrainLayers = layers.ToArray();

            return ToolResult<TerrainPaintResult>.Ok(new TerrainPaintResult
            {
                Action     = "remove-layer",
                InstanceId = UnityIds.Of(terrain.gameObject),
                Name       = terrain.gameObject.name,
                LayerCount = data.terrainLayers.Length,
                Message    = $"Removed terrain layer at index {p.LayerIndex}"
            });
        }

        private static ToolResult<TerrainPaintResult> PaintLayer(
            UnityEngine.Terrain terrain, TerrainData data, TerrainPaintParams p)
        {
            if (data.terrainLayers.Length == 0)
                return ToolResult<TerrainPaintResult>.Fail(
                    "Terrain has no layers. Use add-layer first.", ErrorCodes.NOT_PERMITTED);

            if (p.LayerIndex < 0 || p.LayerIndex >= data.terrainLayers.Length)
                return ToolResult<TerrainPaintResult>.Fail(
                    $"LayerIndex {p.LayerIndex} out of range (0..{data.terrainLayers.Length - 1})",
                    ErrorCodes.OUT_OF_RANGE);

            int alphaRes = data.alphamapResolution;
            int layerCount = data.terrainLayers.Length;

            int centerX = Mathf.Clamp(Mathf.RoundToInt(p.X * (alphaRes - 1)), 0, alphaRes - 1);
            int centerY = Mathf.Clamp(Mathf.RoundToInt(p.Y * (alphaRes - 1)), 0, alphaRes - 1);
            int radius = Mathf.Max(1, p.Radius);

            int xMin = Mathf.Max(0, centerX - radius);
            int yMin = Mathf.Max(0, centerY - radius);
            int xMax = Mathf.Min(alphaRes - 1, centerX + radius);
            int yMax = Mathf.Min(alphaRes - 1, centerY + radius);

            int w = xMax - xMin + 1;
            int h = yMax - yMin + 1;

            var alphas = data.GetAlphamaps(xMin, yMin, w, h);

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

                    // Increase target layer, decrease others proportionally
                    float current = alphas[dy, dx, p.LayerIndex];
                    float newVal = Mathf.Clamp01(current + strength);
                    float diff = newVal - current;

                    alphas[dy, dx, p.LayerIndex] = newVal;

                    // Redistribute remaining weight to other layers
                    float otherTotal = 0f;
                    for (int l = 0; l < layerCount; l++)
                        if (l != p.LayerIndex) otherTotal += alphas[dy, dx, l];

                    if (otherTotal > 0f)
                    {
                        float scale = (1f - newVal) / otherTotal;
                        for (int l = 0; l < layerCount; l++)
                            if (l != p.LayerIndex) alphas[dy, dx, l] *= scale;
                    }
                }
            }

            data.SetAlphamaps(xMin, yMin, alphas);

            return ToolResult<TerrainPaintResult>.Ok(new TerrainPaintResult
            {
                Action     = "paint-layer",
                InstanceId = UnityIds.Of(terrain.gameObject),
                Name       = terrain.gameObject.name,
                LayerCount = layerCount,
                Message    = $"Painted layer {p.LayerIndex} at ({p.X:F2}, {p.Y:F2}) with radius {radius}"
            });
        }

        private static ToolResult<TerrainPaintResult> FillLayer(
            UnityEngine.Terrain terrain, TerrainData data, TerrainPaintParams p)
        {
            if (data.terrainLayers.Length == 0)
                return ToolResult<TerrainPaintResult>.Fail(
                    "Terrain has no layers. Use add-layer first.", ErrorCodes.NOT_PERMITTED);

            if (p.LayerIndex < 0 || p.LayerIndex >= data.terrainLayers.Length)
                return ToolResult<TerrainPaintResult>.Fail(
                    $"LayerIndex {p.LayerIndex} out of range (0..{data.terrainLayers.Length - 1})",
                    ErrorCodes.OUT_OF_RANGE);

            int alphaRes = data.alphamapResolution;
            int layerCount = data.terrainLayers.Length;

            var alphas = new float[alphaRes, alphaRes, layerCount];
            for (int y = 0; y < alphaRes; y++)
                for (int x = 0; x < alphaRes; x++)
                    alphas[y, x, p.LayerIndex] = 1f;

            data.SetAlphamaps(0, 0, alphas);

            return ToolResult<TerrainPaintResult>.Ok(new TerrainPaintResult
            {
                Action     = "fill-layer",
                InstanceId = UnityIds.Of(terrain.gameObject),
                Name       = terrain.gameObject.name,
                LayerCount = layerCount,
                Message    = $"Filled entire terrain with layer {p.LayerIndex}"
            });
        }

        private static ToolResult<TerrainPaintResult> ArrayLayer(
            UnityEngine.Terrain terrain, TerrainData data, TerrainPaintParams p)
        {
            if (data.terrainLayers.Length == 0)
                return ToolResult<TerrainPaintResult>.Fail(
                    "Terrain has no layers. Use add-layer first.", ErrorCodes.NOT_PERMITTED);
            if (p.LayerIndex < 0 || p.LayerIndex >= data.terrainLayers.Length)
                return ToolResult<TerrainPaintResult>.Fail(
                    $"LayerIndex {p.LayerIndex} out of range (0..{data.terrainLayers.Length - 1})",
                    ErrorCodes.OUT_OF_RANGE);
            if (p.Weights == null || p.Weights.Length == 0)
                return ToolResult<TerrainPaintResult>.Fail(
                    "Weights is required for action 'array'", ErrorCodes.INVALID_PARAM);
            if (p.Width <= 0 || p.HeightCells <= 0)
                return ToolResult<TerrainPaintResult>.Fail(
                    "Width and HeightCells must be > 0 for action 'array'", ErrorCodes.INVALID_PARAM);
            if (p.Weights.Length != p.Width * p.HeightCells)
                return ToolResult<TerrainPaintResult>.Fail(
                    $"Weights length {p.Weights.Length} does not match Width*HeightCells ({p.Width * p.HeightCells})",
                    ErrorCodes.INVALID_PARAM);

            int alphaRes = data.alphamapResolution;
            if (p.ArrayX < 0 || p.ArrayY < 0 || p.ArrayX + p.Width > alphaRes || p.ArrayY + p.HeightCells > alphaRes)
                return ToolResult<TerrainPaintResult>.Fail(
                    $"Array region ({p.ArrayX},{p.ArrayY})+({p.Width}x{p.HeightCells}) exceeds alphamap resolution {alphaRes}",
                    ErrorCodes.INVALID_PARAM);

            int layerCount = data.terrainLayers.Length;
            var alphas = data.GetAlphamaps(p.ArrayX, p.ArrayY, p.Width, p.HeightCells);

            for (int y = 0; y < p.HeightCells; y++)
            {
                for (int x = 0; x < p.Width; x++)
                {
                    SetLayerWeightAndRedistribute(alphas, x, y, p.LayerIndex, layerCount,
                        Mathf.Clamp01(p.Weights[y * p.Width + x]));
                }
            }

            data.SetAlphamaps(p.ArrayX, p.ArrayY, alphas);

            return ToolResult<TerrainPaintResult>.Ok(new TerrainPaintResult
            {
                Action     = "array",
                InstanceId = UnityIds.Of(terrain.gameObject),
                Name       = terrain.gameObject.name,
                LayerCount = layerCount,
                Message    = $"Applied {p.Width}x{p.HeightCells} weight array to layer {p.LayerIndex} at ({p.ArrayX},{p.ArrayY})"
            });
        }

        private static ToolResult<TerrainPaintResult> AutoLayer(
            UnityEngine.Terrain terrain, TerrainData data, TerrainPaintParams p)
        {
            if (data.terrainLayers.Length == 0)
                return ToolResult<TerrainPaintResult>.Fail(
                    "Terrain has no layers. Use add-layer first.", ErrorCodes.NOT_PERMITTED);
            if (p.LayerIndex < 0 || p.LayerIndex >= data.terrainLayers.Length)
                return ToolResult<TerrainPaintResult>.Fail(
                    $"LayerIndex {p.LayerIndex} out of range (0..{data.terrainLayers.Length - 1})",
                    ErrorCodes.OUT_OF_RANGE);
            if (!p.MinSlope.HasValue && !p.MaxSlope.HasValue && !p.MinHeight.HasValue && !p.MaxHeight.HasValue)
                return ToolResult<TerrainPaintResult>.Fail(
                    "At least one of MinSlope/MaxSlope/MinHeight/MaxHeight is required for action 'auto'",
                    ErrorCodes.INVALID_PARAM);

            int alphaRes = data.alphamapResolution;
            int layerCount = data.terrainLayers.Length;
            var alphas = data.GetAlphamaps(0, 0, alphaRes, alphaRes);
            int painted = 0;

            for (int y = 0; y < alphaRes; y++)
            {
                for (int x = 0; x < alphaRes; x++)
                {
                    float nx = (float)x / (alphaRes - 1);
                    float ny = (float)y / (alphaRes - 1);

                    if (p.MinSlope.HasValue || p.MaxSlope.HasValue)
                    {
                        var slope = data.GetSteepness(nx, ny);
                        if (p.MinSlope.HasValue && slope < p.MinSlope.Value) continue;
                        if (p.MaxSlope.HasValue && slope > p.MaxSlope.Value) continue;
                    }
                    if (p.MinHeight.HasValue || p.MaxHeight.HasValue)
                    {
                        var height = data.GetInterpolatedHeight(nx, ny);
                        if (p.MinHeight.HasValue && height < p.MinHeight.Value) continue;
                        if (p.MaxHeight.HasValue && height > p.MaxHeight.Value) continue;
                    }

                    SetLayerWeightAndRedistribute(alphas, x, y, p.LayerIndex, layerCount, 1f);
                    painted++;
                }
            }

            data.SetAlphamaps(0, 0, alphas);

            return ToolResult<TerrainPaintResult>.Ok(new TerrainPaintResult
            {
                Action     = "auto",
                InstanceId = UnityIds.Of(terrain.gameObject),
                Name       = terrain.gameObject.name,
                LayerCount = layerCount,
                Message    = $"Auto-painted layer {p.LayerIndex} on {painted} of {alphaRes * alphaRes} alphamap samples"
            });
        }

        /// <summary>Sets alphas[y,x,layerIndex] to newVal and rescales every other layer at that
        /// pixel proportionally so the total stays 1 — same redistribution PaintLayer already uses.</summary>
        private static void SetLayerWeightAndRedistribute(float[,,] alphas, int x, int y, int layerIndex, int layerCount, float newVal)
        {
            alphas[y, x, layerIndex] = newVal;

            float otherTotal = 0f;
            for (int l = 0; l < layerCount; l++)
                if (l != layerIndex) otherTotal += alphas[y, x, l];

            if (otherTotal > 0f)
            {
                float scale = (1f - newVal) / otherTotal;
                for (int l = 0; l < layerCount; l++)
                    if (l != layerIndex) alphas[y, x, l] *= scale;
            }
        }
    }
}

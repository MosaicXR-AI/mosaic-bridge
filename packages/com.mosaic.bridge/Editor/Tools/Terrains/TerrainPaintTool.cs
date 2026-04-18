using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Terrains
{
    public static class TerrainPaintTool
    {
        [MosaicTool("terrain/paint",
                    "Splatmap painting: add-layer, remove-layer, paint-layer, fill-layer",
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

                default:
                    return ToolResult<TerrainPaintResult>.Fail(
                        $"Unknown action '{p.Action}'. Valid actions: add-layer, remove-layer, paint-layer, fill-layer",
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

            var layer = new TerrainLayer
            {
                diffuseTexture = texture,
                normalMapTexture = normalMap,
                tileSize = p.TileSize != null && p.TileSize.Length == 2
                    ? new Vector2(p.TileSize[0], p.TileSize[1])
                    : new Vector2(15f, 15f)
            };

            // Save layer as asset
            string layerPath = $"Assets/TerrainData/{terrain.gameObject.name}_Layer{data.terrainLayers.Length}.terrainlayer";
            string dir = System.IO.Path.GetDirectoryName(layerPath);
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets", "TerrainData");
            AssetDatabase.CreateAsset(layer, layerPath);

            var layers = new List<TerrainLayer>(data.terrainLayers);
            layers.Add(layer);
            data.terrainLayers = layers.ToArray();

            return ToolResult<TerrainPaintResult>.Ok(new TerrainPaintResult
            {
                Action     = "add-layer",
                InstanceId = terrain.gameObject.GetInstanceID(),
                Name       = terrain.gameObject.name,
                LayerCount = data.terrainLayers.Length,
                Message    = $"Added terrain layer from '{p.TexturePath}' (index {data.terrainLayers.Length - 1})"
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
                InstanceId = terrain.gameObject.GetInstanceID(),
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
                InstanceId = terrain.gameObject.GetInstanceID(),
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
                InstanceId = terrain.gameObject.GetInstanceID(),
                Name       = terrain.gameObject.name,
                LayerCount = layerCount,
                Message    = $"Filled entire terrain with layer {p.LayerIndex}"
            });
        }
    }
}

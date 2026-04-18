using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Terrains
{
    public static class TerrainDetailTool
    {
        [MosaicTool("terrain/detail",
                    "Detail/grass management: add-prototype, paint, scatter, clear",
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

                default:
                    return ToolResult<TerrainDetailResult>.Fail(
                        $"Unknown action '{p.Action}'. Valid actions: add-prototype, paint, scatter, clear",
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

            var prototypes = new List<DetailPrototype>(data.detailPrototypes);
            prototypes.Add(prototype);
            data.detailPrototypes = prototypes.ToArray();

            return ToolResult<TerrainDetailResult>.Ok(new TerrainDetailResult
            {
                Action         = "add-prototype",
                InstanceId     = terrain.gameObject.GetInstanceID(),
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
                InstanceId     = terrain.gameObject.GetInstanceID(),
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

            for (int y = 0; y < detailRes; y++)
            {
                for (int x = 0; x < detailRes; x++)
                {
                    if (rng.NextDouble() < 0.3)
                        layer[y, x] = Mathf.Clamp(p.Density, 0, 16);
                }
            }

            data.SetDetailLayer(0, 0, p.PrototypeIndex, layer);
            terrain.Flush();

            return ToolResult<TerrainDetailResult>.Ok(new TerrainDetailResult
            {
                Action         = "scatter",
                InstanceId     = terrain.gameObject.GetInstanceID(),
                Name           = terrain.gameObject.name,
                PrototypeCount = data.detailPrototypes.Length,
                Message        = $"Scattered detail {p.PrototypeIndex} across terrain with seed {p.Seed}"
            });
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
                InstanceId     = terrain.gameObject.GetInstanceID(),
                Name           = terrain.gameObject.name,
                PrototypeCount = data.detailPrototypes.Length,
                Message        = startLayer + 1 == endLayer
                    ? $"Cleared detail layer {startLayer}"
                    : $"Cleared all {endLayer} detail layers"
            });
        }
    }
}

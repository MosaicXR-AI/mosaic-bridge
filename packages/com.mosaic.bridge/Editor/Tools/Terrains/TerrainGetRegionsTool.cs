using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Terrains
{
    public static class TerrainGetRegionsTool
    {
        [MosaicTool("terrain/get-regions",
                    "Reads the terrain alphamap (splatmap) and returns per-layer coverage statistics: " +
                    "what fraction of the terrain is dominated by each layer, and the world-space bounding box " +
                    "of that layer's painted area. Use this before scene/plan-composition to understand the current " +
                    "texture layout — which regions are sand, rock, grass, etc.",
                    isReadOnly: true)]
        public static ToolResult<TerrainGetRegionsResult> Execute(TerrainGetRegionsParams p)
        {
            UnityEngine.Terrain terrain;
            if (p.InstanceId != 0 || !string.IsNullOrEmpty(p.TerrainName))
            {
                terrain = TerrainToolHelpers.ResolveTerrain(p.InstanceId, p.TerrainName, out string err);
                if (terrain == null)
                    return ToolResult<TerrainGetRegionsResult>.Fail(err, ErrorCodes.NOT_FOUND);
            }
            else
            {
                terrain = UnityEngine.Terrain.activeTerrain;
                if (terrain == null)
                    return ToolResult<TerrainGetRegionsResult>.Fail(
                        "No active terrain in scene. Provide TerrainName or InstanceId.",
                        ErrorCodes.NOT_FOUND);
            }

            var data       = terrain.terrainData;
            int layerCount = data.terrainLayers?.Length ?? 0;
            if (layerCount == 0)
                return ToolResult<TerrainGetRegionsResult>.Ok(new TerrainGetRegionsResult
                {
                    TerrainName = terrain.name,
                    InstanceId  = terrain.gameObject.GetInstanceID(),
                    LayerCount  = 0,
                    Regions     = new TerrainRegion[0]
                });

            int alphaRes  = data.alphamapResolution;
            var alphas    = data.GetAlphamaps(0, 0, alphaRes, alphaRes);
            var terrainPos = terrain.transform.position;
            float terrainW = data.size.x;
            float terrainL = data.size.z;

            // Per-layer: dominant cell count + bounding box in alphamap coords
            var counts   = new int[layerCount];
            var xMins    = new int[layerCount];
            var xMaxs    = new int[layerCount];
            var zMins    = new int[layerCount];
            var zMaxs    = new int[layerCount];

            for (int l = 0; l < layerCount; l++)
            {
                xMins[l] = alphaRes;
                zMins[l] = alphaRes;
                xMaxs[l] = -1;
                zMaxs[l] = -1;
            }

            int totalCells = alphaRes * alphaRes;

            for (int z = 0; z < alphaRes; z++)
            {
                for (int x = 0; x < alphaRes; x++)
                {
                    // Find the dominant layer at this cell
                    int   domLayer = 0;
                    float domVal   = alphas[z, x, 0];
                    for (int l = 1; l < layerCount; l++)
                    {
                        if (alphas[z, x, l] > domVal)
                        {
                            domVal   = alphas[z, x, l];
                            domLayer = l;
                        }
                    }

                    counts[domLayer]++;
                    if (x < xMins[domLayer]) xMins[domLayer] = x;
                    if (x > xMaxs[domLayer]) xMaxs[domLayer] = x;
                    if (z < zMins[domLayer]) zMins[domLayer] = z;
                    if (z > zMaxs[domLayer]) zMaxs[domLayer] = z;
                }
            }

            // Convert alphamap coords to world space
            float cellW = terrainW / alphaRes;
            float cellL = terrainL / alphaRes;

            var regions = new List<TerrainRegion>(layerCount);
            for (int l = 0; l < layerCount; l++)
            {
                float coverage = (float)counts[l] / totalCells;
                if (coverage < p.MinCoverageThreshold)
                    continue;

                string texPath = "";
                var layer = data.terrainLayers[l];
                if (layer?.diffuseTexture != null)
                    texPath = AssetDatabase.GetAssetPath(layer.diffuseTexture);

                float wxMin = terrainPos.x + xMins[l] * cellW;
                float wxMax = terrainPos.x + (xMaxs[l] + 1) * cellW;
                float wzMin = terrainPos.z + zMins[l] * cellL;
                float wzMax = terrainPos.z + (zMaxs[l] + 1) * cellL;

                regions.Add(new TerrainRegion
                {
                    LayerIndex       = l,
                    TexturePath      = texPath,
                    CoverageFraction = coverage,
                    CoveragePercent  = coverage * 100f,
                    WorldXMin        = wxMin,
                    WorldXMax        = wxMax,
                    WorldZMin        = wzMin,
                    WorldZMax        = wzMax,
                    CenterWorldX     = (wxMin + wxMax) * 0.5f,
                    CenterWorldZ     = (wzMin + wzMax) * 0.5f
                });
            }

            return ToolResult<TerrainGetRegionsResult>.Ok(new TerrainGetRegionsResult
            {
                TerrainName = terrain.name,
                InstanceId  = terrain.gameObject.GetInstanceID(),
                LayerCount  = layerCount,
                Regions     = regions.ToArray()
            });
        }
    }
}

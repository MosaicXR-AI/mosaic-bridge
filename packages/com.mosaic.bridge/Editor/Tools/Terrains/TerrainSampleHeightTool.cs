using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Terrains
{
    public static class TerrainSampleHeightTool
    {
        [MosaicTool("terrain/sample-height",
                    "Returns the world-space Y (height in meters) of the terrain surface at a given world XZ position. " +
                    "Use this before every gameobject/create or prefab/instantiate call to resolve the correct Y coordinate. " +
                    "The result WorldY + your desired offset is the placement Y. Never use Y=0 for objects placed on terrain.",
                    isReadOnly: true)]
        public static ToolResult<TerrainSampleHeightResult> Execute(TerrainSampleHeightParams p)
        {
            UnityEngine.Terrain terrain;

            if (p.InstanceId != 0 || !string.IsNullOrEmpty(p.TerrainName))
            {
                terrain = TerrainToolHelpers.ResolveTerrain(p.InstanceId, p.TerrainName, out string err);
                if (terrain == null)
                    return ToolResult<TerrainSampleHeightResult>.Fail(err, ErrorCodes.NOT_FOUND);
            }
            else
            {
                terrain = UnityEngine.Terrain.activeTerrain;
                if (terrain == null)
                    return ToolResult<TerrainSampleHeightResult>.Fail(
                        "No active terrain found in the scene. Create a terrain first with terrain/create, " +
                        "or provide TerrainName/InstanceId to target a specific terrain.",
                        ErrorCodes.NOT_FOUND);
            }

            float worldY = terrain.SampleHeight(new Vector3(p.WorldX, 0f, p.WorldZ));
            float maxHeight = terrain.terrainData.size.y;
            float normalizedHeight = maxHeight > 0f ? worldY / maxHeight : 0f;
            var size = terrain.terrainData.size;

            return ToolResult<TerrainSampleHeightResult>.Ok(new TerrainSampleHeightResult
            {
                WorldY            = worldY,
                NormalizedHeight  = normalizedHeight,
                TerrainName       = terrain.name,
                // Suggest placement Y with a 0.1m offset to avoid z-fighting
                SuggestedPlacementY = new[] { p.WorldX, worldY + 0.1f, p.WorldZ },
                TerrainSize       = new[] { size.x, size.y, size.z }
            });
        }
    }
}

using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Terrains
{
    public static class TerrainInfoTool
    {
        [MosaicTool("terrain/info",
                    "Queries terrain state: size, resolution, layer/tree/detail counts",
                    isReadOnly: true)]
        public static ToolResult<TerrainInfoResult> Execute(TerrainInfoParams p)
        {
            var terrain = TerrainToolHelpers.ResolveTerrain(p.InstanceId, p.Name, out string error);
            if (terrain == null)
                return ToolResult<TerrainInfoResult>.Fail(error, ErrorCodes.NOT_FOUND);

            var data = terrain.terrainData;
            var pos = terrain.gameObject.transform.position;
            string assetPath = AssetDatabase.GetAssetPath(data);

            return ToolResult<TerrainInfoResult>.Ok(new TerrainInfoResult
            {
                InstanceId          = terrain.gameObject.GetInstanceID(),
                Name                = terrain.gameObject.name,
                Width               = data.size.x,
                Length              = data.size.z,
                Height              = data.size.y,
                HeightmapResolution = data.heightmapResolution,
                AlphamapResolution  = data.alphamapResolution,
                DetailResolution    = data.detailResolution,
                LayerCount          = data.terrainLayers.Length,
                TreePrototypeCount  = data.treePrototypes.Length,
                TreeInstanceCount   = data.treeInstances.Length,
                DetailPrototypeCount = data.detailPrototypes.Length,
                Position            = new[] { pos.x, pos.y, pos.z },
                TerrainDataAssetPath = assetPath
            });
        }
    }
}

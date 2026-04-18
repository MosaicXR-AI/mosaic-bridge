using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Terrains
{
    public static class TerrainTreesTool
    {
        [MosaicTool("terrain/trees",
                    "Tree management: add-prototype, place, clear, get-instances",
                    isReadOnly: false)]
        public static ToolResult<TerrainTreesResult> Execute(TerrainTreesParams p)
        {
            var terrain = TerrainToolHelpers.ResolveTerrain(p.InstanceId, p.Name, out string error);
            if (terrain == null)
                return ToolResult<TerrainTreesResult>.Fail(error, ErrorCodes.NOT_FOUND);

            var data = terrain.terrainData;

            switch (p.Action?.ToLowerInvariant())
            {
                case "add-prototype":
                    return AddPrototype(terrain, data, p);

                case "place":
                    return PlaceTrees(terrain, data, p);

                case "clear":
                    return ClearTrees(terrain, data);

                case "get-instances":
                    return GetInstances(terrain, data);

                default:
                    return ToolResult<TerrainTreesResult>.Fail(
                        $"Unknown action '{p.Action}'. Valid actions: add-prototype, place, clear, get-instances",
                        ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<TerrainTreesResult> AddPrototype(
            UnityEngine.Terrain terrain, TerrainData data, TerrainTreesParams p)
        {
            if (string.IsNullOrEmpty(p.PrefabPath))
                return ToolResult<TerrainTreesResult>.Fail(
                    "PrefabPath is required for add-prototype", ErrorCodes.INVALID_PARAM);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p.PrefabPath);
            if (prefab == null)
                return ToolResult<TerrainTreesResult>.Fail(
                    $"Prefab not found at '{p.PrefabPath}'", ErrorCodes.NOT_FOUND);

            Undo.RegisterCompleteObjectUndo(data, "Mosaic: Terrain Add Tree Prototype");

            var prototypes = new List<TreePrototype>(data.treePrototypes);
            prototypes.Add(new TreePrototype { prefab = prefab });
            data.treePrototypes = prototypes.ToArray();

            return ToolResult<TerrainTreesResult>.Ok(new TerrainTreesResult
            {
                Action         = "add-prototype",
                InstanceId     = terrain.gameObject.GetInstanceID(),
                Name           = terrain.gameObject.name,
                PrototypeCount = data.treePrototypes.Length,
                TreeCount      = data.treeInstances.Length,
                Message        = $"Added tree prototype from '{p.PrefabPath}' (index {data.treePrototypes.Length - 1})"
            });
        }

        private static ToolResult<TerrainTreesResult> PlaceTrees(
            UnityEngine.Terrain terrain, TerrainData data, TerrainTreesParams p)
        {
            if (data.treePrototypes.Length == 0)
                return ToolResult<TerrainTreesResult>.Fail(
                    "Terrain has no tree prototypes. Use add-prototype first.", ErrorCodes.NOT_PERMITTED);

            if (p.PrototypeIndex < 0 || p.PrototypeIndex >= data.treePrototypes.Length)
                return ToolResult<TerrainTreesResult>.Fail(
                    $"PrototypeIndex {p.PrototypeIndex} out of range (0..{data.treePrototypes.Length - 1})",
                    ErrorCodes.OUT_OF_RANGE);

            Undo.RegisterCompleteObjectUndo(data, "Mosaic: Terrain Place Trees");

            var instances = new List<TreeInstance>(data.treeInstances);
            int count = Mathf.Max(1, p.Count);

            if (p.Position != null && p.Position.Length >= 3 && count == 1)
            {
                // Single placement at specified position
                instances.Add(new TreeInstance
                {
                    prototypeIndex = p.PrototypeIndex,
                    position       = new Vector3(p.Position[0], p.Position[1], p.Position[2]),
                    widthScale     = p.WidthScale,
                    heightScale    = p.HeightScale,
                    color          = Color.white,
                    lightmapColor  = Color.white,
                    rotation       = 0f
                });
            }
            else
            {
                // Batch random placement
                var rng = new System.Random(p.Seed);
                for (int i = 0; i < count; i++)
                {
                    float x = (float)rng.NextDouble();
                    float z = (float)rng.NextDouble();
                    float y = data.GetInterpolatedHeight(x, z) / data.size.y;

                    instances.Add(new TreeInstance
                    {
                        prototypeIndex = p.PrototypeIndex,
                        position       = new Vector3(x, y, z),
                        widthScale     = p.WidthScale,
                        heightScale    = p.HeightScale,
                        color          = Color.white,
                        lightmapColor  = Color.white,
                        rotation       = (float)(rng.NextDouble() * 360.0)
                    });
                }
            }

            data.SetTreeInstances(instances.ToArray(), snapToHeightmap: true);
            terrain.Flush();

            return ToolResult<TerrainTreesResult>.Ok(new TerrainTreesResult
            {
                Action         = "place",
                InstanceId     = terrain.gameObject.GetInstanceID(),
                Name           = terrain.gameObject.name,
                PrototypeCount = data.treePrototypes.Length,
                TreeCount      = data.treeInstances.Length,
                Message        = $"Placed {count} tree(s) using prototype {p.PrototypeIndex}"
            });
        }

        private static ToolResult<TerrainTreesResult> ClearTrees(
            UnityEngine.Terrain terrain, TerrainData data)
        {
            Undo.RegisterCompleteObjectUndo(data, "Mosaic: Terrain Clear Trees");

            int removed = data.treeInstances.Length;
            data.SetTreeInstances(new TreeInstance[0], snapToHeightmap: false);
            terrain.Flush();

            return ToolResult<TerrainTreesResult>.Ok(new TerrainTreesResult
            {
                Action         = "clear",
                InstanceId     = terrain.gameObject.GetInstanceID(),
                Name           = terrain.gameObject.name,
                PrototypeCount = data.treePrototypes.Length,
                TreeCount      = 0,
                Message        = $"Cleared {removed} tree instance(s)"
            });
        }

        private static ToolResult<TerrainTreesResult> GetInstances(
            UnityEngine.Terrain terrain, TerrainData data)
        {
            return ToolResult<TerrainTreesResult>.Ok(new TerrainTreesResult
            {
                Action         = "get-instances",
                InstanceId     = terrain.gameObject.GetInstanceID(),
                Name           = terrain.gameObject.name,
                PrototypeCount = data.treePrototypes.Length,
                TreeCount      = data.treeInstances.Length,
                Message        = $"Terrain has {data.treePrototypes.Length} prototype(s) and {data.treeInstances.Length} instance(s)"
            });
        }
    }
}

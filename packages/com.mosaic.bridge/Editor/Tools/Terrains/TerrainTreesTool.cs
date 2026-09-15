using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Terrains
{
    public static class TerrainTreesTool
    {
        [MosaicTool("terrain/trees",
                    "Tree management: add-prototype, place (single Position, or batch Count with " +
                    "optional masked scatter — MinSlope/MaxSlope degrees, MinHeight/MaxHeight, " +
                    "RequiredLayerIndex+MinLayerWeight — for forests avoiding roads/water/cliffs), " +
                    "place-list (explicit Positions/Rotations/Colors), clear, get-instances " +
                    "(now returns Instances with positions).",
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

                case "place-list":
                    return PlaceList(terrain, data, p);

                case "clear":
                    return ClearTrees(terrain, data);

                case "get-instances":
                    return GetInstances(terrain, data);

                default:
                    return ToolResult<TerrainTreesResult>.Fail(
                        $"Unknown action '{p.Action}'. Valid actions: add-prototype, place, place-list, clear, get-instances",
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

            // Unity's terrain tree system renders instances by sampling the
            // ROOT-level MeshRenderer / LODGroup of the prototype. Prefabs
            // whose visuals live on a nested child GameObject produce
            // invisible tree instances (the terrain places them at correct
            // positions in data but nothing draws). Reject with a clear
            // message so the caller either fixes the prefab or uses
            // gameobject/create + component/add as a scattering alternative.
            var hasRootMesh = prefab.GetComponent<MeshRenderer>() != null
                           || prefab.GetComponent<LODGroup>() != null
                           || prefab.GetComponent<BillboardRenderer>() != null;
            if (!hasRootMesh)
                return ToolResult<TerrainTreesResult>.Fail(
                    $"Prefab '{p.PrefabPath}' has no MeshRenderer, LODGroup, or BillboardRenderer on its ROOT GameObject. " +
                    "Unity's terrain tree system renders from the prototype root only — nested-child visuals will not draw. " +
                    "Either flatten the prefab hierarchy, add a root MeshRenderer, or use gameobject/create to scatter prefabs as scene objects.",
                    ErrorCodes.INVALID_PARAM,
                    "See Unity Manual: Trees > Tree Prototypes. Terrain tree rendering is a special code path distinct from normal prefab instantiation.");

            Undo.RegisterCompleteObjectUndo(data, "Mosaic: Terrain Add Tree Prototype");

            var prototypes = new List<TreePrototype>(data.treePrototypes);
            prototypes.Add(new TreePrototype { prefab = prefab });
            data.treePrototypes = prototypes.ToArray();

            return ToolResult<TerrainTreesResult>.Ok(new TerrainTreesResult
            {
                Action         = "add-prototype",
                InstanceId     = UnityIds.Of(terrain.gameObject),
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
                // Batch random placement, optionally masked by slope/height/layer — the fix for
                // "forests that avoid roads/water/cliffs" needing to be built one tree at a time.
                var rng = new System.Random(p.Seed);
                bool masked = p.MinSlope.HasValue || p.MaxSlope.HasValue ||
                              p.MinHeight.HasValue || p.MaxHeight.HasValue || p.RequiredLayerIndex.HasValue;
                int maxAttempts = p.MaxAttempts ?? count * 20;
                int placed = 0;

                for (int attempt = 0; attempt < maxAttempts && placed < count; attempt++)
                {
                    float x = (float)rng.NextDouble();
                    float z = (float)rng.NextDouble();

                    if (masked && !PassesMask(data, x, z, p))
                        continue;

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
                    placed++;
                }

                data.SetTreeInstances(instances.ToArray(), snapToHeightmap: true);
                terrain.Flush();

                return ToolResult<TerrainTreesResult>.Ok(new TerrainTreesResult
                {
                    Action         = "place",
                    InstanceId     = UnityIds.Of(terrain.gameObject),
                    Name           = terrain.gameObject.name,
                    PrototypeCount = data.treePrototypes.Length,
                    TreeCount      = data.treeInstances.Length,
                    PlacedCount    = placed,
                    Message        = masked
                        ? $"Placed {placed} of {count} requested tree(s) using prototype {p.PrototypeIndex} (masked scatter)"
                        : $"Placed {placed} tree(s) using prototype {p.PrototypeIndex}"
                });
            }

            data.SetTreeInstances(instances.ToArray(), snapToHeightmap: true);
            terrain.Flush();

            return ToolResult<TerrainTreesResult>.Ok(new TerrainTreesResult
            {
                Action         = "place",
                InstanceId     = UnityIds.Of(terrain.gameObject),
                Name           = terrain.gameObject.name,
                PrototypeCount = data.treePrototypes.Length,
                TreeCount      = data.treeInstances.Length,
                PlacedCount    = 1,
                Message        = $"Placed {count} tree(s) using prototype {p.PrototypeIndex}"
            });
        }

        private static bool PassesMask(TerrainData data, float x, float z, TerrainTreesParams p)
        {
            if (p.MinSlope.HasValue || p.MaxSlope.HasValue)
            {
                var slope = data.GetSteepness(x, z);
                if (p.MinSlope.HasValue && slope < p.MinSlope.Value) return false;
                if (p.MaxSlope.HasValue && slope > p.MaxSlope.Value) return false;
            }
            if (p.MinHeight.HasValue || p.MaxHeight.HasValue)
            {
                var height = data.GetInterpolatedHeight(x, z);
                if (p.MinHeight.HasValue && height < p.MinHeight.Value) return false;
                if (p.MaxHeight.HasValue && height > p.MaxHeight.Value) return false;
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

        private static ToolResult<TerrainTreesResult> PlaceList(
            UnityEngine.Terrain terrain, TerrainData data, TerrainTreesParams p)
        {
            if (data.treePrototypes.Length == 0)
                return ToolResult<TerrainTreesResult>.Fail(
                    "Terrain has no tree prototypes. Use add-prototype first.", ErrorCodes.NOT_PERMITTED);
            if (p.PrototypeIndex < 0 || p.PrototypeIndex >= data.treePrototypes.Length)
                return ToolResult<TerrainTreesResult>.Fail(
                    $"PrototypeIndex {p.PrototypeIndex} out of range (0..{data.treePrototypes.Length - 1})",
                    ErrorCodes.OUT_OF_RANGE);
            if (p.Positions == null || p.Positions.Length == 0 || p.Positions.Length % 3 != 0)
                return ToolResult<TerrainTreesResult>.Fail(
                    "Positions must be a non-empty flat array of [x,y,z, x,y,z, ...]", ErrorCodes.INVALID_PARAM);

            int n = p.Positions.Length / 3;
            if (p.Rotations != null && p.Rotations.Length != n)
                return ToolResult<TerrainTreesResult>.Fail(
                    $"Rotations length {p.Rotations.Length} does not match tree count {n}", ErrorCodes.INVALID_PARAM);
            if (p.Colors != null && p.Colors.Length != n * 3)
                return ToolResult<TerrainTreesResult>.Fail(
                    $"Colors length {p.Colors.Length} does not match tree count*3 ({n * 3})", ErrorCodes.INVALID_PARAM);

            Undo.RegisterCompleteObjectUndo(data, "Mosaic: Terrain Place Tree List");
            var instances = new List<TreeInstance>(data.treeInstances);

            for (int i = 0; i < n; i++)
            {
                float x = p.Positions[i * 3];
                float z = p.Positions[i * 3 + 2];
                float y = data.GetInterpolatedHeight(x, z) / data.size.y;
                var color = p.Colors != null
                    ? new Color(p.Colors[i * 3], p.Colors[i * 3 + 1], p.Colors[i * 3 + 2])
                    : Color.white;

                instances.Add(new TreeInstance
                {
                    prototypeIndex = p.PrototypeIndex,
                    position       = new Vector3(x, y, z),
                    widthScale     = p.WidthScale,
                    heightScale    = p.HeightScale,
                    color          = color,
                    lightmapColor  = Color.white,
                    rotation       = p.Rotations != null ? p.Rotations[i] : 0f,
                });
            }

            data.SetTreeInstances(instances.ToArray(), snapToHeightmap: true);
            terrain.Flush();

            return ToolResult<TerrainTreesResult>.Ok(new TerrainTreesResult
            {
                Action         = "place-list",
                InstanceId     = UnityIds.Of(terrain.gameObject),
                Name           = terrain.gameObject.name,
                PrototypeCount = data.treePrototypes.Length,
                TreeCount      = data.treeInstances.Length,
                PlacedCount    = n,
                Message        = $"Placed {n} tree(s) from an explicit list using prototype {p.PrototypeIndex}"
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
                InstanceId     = UnityIds.Of(terrain.gameObject),
                Name           = terrain.gameObject.name,
                PrototypeCount = data.treePrototypes.Length,
                TreeCount      = 0,
                Message        = $"Cleared {removed} tree instance(s)"
            });
        }

        private static ToolResult<TerrainTreesResult> GetInstances(
            UnityEngine.Terrain terrain, TerrainData data)
        {
            var instances = new TerrainTreeInstanceInfo[data.treeInstances.Length];
            for (int i = 0; i < instances.Length; i++)
            {
                var t = data.treeInstances[i];
                instances[i] = new TerrainTreeInstanceInfo
                {
                    PrototypeIndex = t.prototypeIndex,
                    Position = new[] { t.position.x, t.position.y, t.position.z },
                    WidthScale = t.widthScale,
                    HeightScale = t.heightScale,
                    Rotation = t.rotation,
                };
            }

            return ToolResult<TerrainTreesResult>.Ok(new TerrainTreesResult
            {
                Action         = "get-instances",
                InstanceId     = UnityIds.Of(terrain.gameObject),
                Name           = terrain.gameObject.name,
                PrototypeCount = data.treePrototypes.Length,
                TreeCount      = data.treeInstances.Length,
                Instances      = instances,
                Message        = $"Terrain has {data.treePrototypes.Length} prototype(s) and {data.treeInstances.Length} instance(s)"
            });
        }
    }
}

using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Terrains
{
    internal static class TerrainToolHelpers
    {
        /// <summary>
        /// Resolves a Terrain component by InstanceId or Name.
        /// Returns null and sets the fail result if not found.
        /// </summary>
        internal static UnityEngine.Terrain ResolveTerrain(int instanceId, string name, out string error)
        {
            error = null;

            // Prefer InstanceId lookup
            if (instanceId != 0)
            {
                var obj = UnityEngine.Resources.EntityIdToObject(instanceId) as GameObject;
                if (obj == null)
                {
                    error = $"No GameObject found with InstanceId {instanceId}";
                    return null;
                }
                var t = obj.GetComponent<UnityEngine.Terrain>();
                if (t == null)
                {
                    error = $"GameObject '{obj.name}' (InstanceId {instanceId}) has no Terrain component";
                    return null;
                }
                return t;
            }

            // Fallback to name lookup
            if (!string.IsNullOrEmpty(name))
            {
                var go = GameObject.Find(name);
                if (go == null)
                {
                    error = $"No GameObject found with name '{name}'";
                    return null;
                }
                var t = go.GetComponent<UnityEngine.Terrain>();
                if (t == null)
                {
                    error = $"GameObject '{name}' has no Terrain component";
                    return null;
                }
                return t;
            }

            error = "Either InstanceId or Name must be provided";
            return null;
        }

        /// <summary>
        /// Ensures the TerrainData asset directory exists and returns the asset path.
        /// </summary>
        internal static string GetTerrainDataAssetPath(string terrainName)
        {
            const string dir = "Assets/TerrainData";
            if (!AssetDatabase.IsValidFolder(dir))
                AssetDatabase.CreateFolder("Assets", "TerrainData");
            return $"{dir}/{terrainName}_TerrainData.asset";
        }

        /// <summary>
        /// Clamps an int to the nearest valid heightmap resolution (power of 2 + 1).
        /// Valid values: 33, 65, 129, 257, 513, 1025, 2049, 4097.
        /// </summary>
        internal static int ClampHeightmapResolution(int value)
        {
            int[] valid = { 33, 65, 129, 257, 513, 1025, 2049, 4097 };
            int best = valid[0];
            int bestDist = Mathf.Abs(value - best);
            for (int i = 1; i < valid.Length; i++)
            {
                int dist = Mathf.Abs(value - valid[i]);
                if (dist < bestDist)
                {
                    best = valid[i];
                    bestDist = dist;
                }
            }
            return best;
        }
    }
}

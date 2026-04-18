using System.IO;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Assets
{
    public static class AssetCreatePrefabTool
    {
        [MosaicTool("asset/create_prefab",
                    "Saves a scene GameObject as a prefab asset at the specified project path",
                    isReadOnly: false)]
        public static ToolResult<AssetCreatePrefabResult> Execute(AssetCreatePrefabParams p)
        {
            if (string.IsNullOrEmpty(p.GameObjectName))
                return ToolResult<AssetCreatePrefabResult>.Fail(
                    "GameObjectName is required", ErrorCodes.INVALID_PARAM);
            if (string.IsNullOrEmpty(p.PrefabPath))
                return ToolResult<AssetCreatePrefabResult>.Fail(
                    "PrefabPath is required", ErrorCodes.INVALID_PARAM);

            var go = GameObject.Find(p.GameObjectName);
            if (go == null)
                return ToolResult<AssetCreatePrefabResult>.Fail(
                    $"GameObject '{p.GameObjectName}' not found", ErrorCodes.NOT_FOUND);

            bool existed = AssetDatabase.AssetPathExists(p.PrefabPath);
            if (existed && !p.OverwriteExisting)
                return ToolResult<AssetCreatePrefabResult>.Fail(
                    $"Prefab already exists at '{p.PrefabPath}'. Set OverwriteExisting=true to replace it.",
                    ErrorCodes.CONFLICT);

            // Ensure directory exists
            var absoluteDir = Path.GetDirectoryName(
                Path.Combine(Application.dataPath, "..", p.PrefabPath));
            if (!string.IsNullOrEmpty(absoluteDir))
                Directory.CreateDirectory(absoluteDir);

            var prefabAsset = PrefabUtility.SaveAsPrefabAsset(go, p.PrefabPath);
            if (prefabAsset == null)
                return ToolResult<AssetCreatePrefabResult>.Fail(
                    $"PrefabUtility.SaveAsPrefabAsset returned null for path '{p.PrefabPath}'",
                    ErrorCodes.INTERNAL_ERROR);

            return ToolResult<AssetCreatePrefabResult>.Ok(new AssetCreatePrefabResult
            {
                PrefabPath     = p.PrefabPath,
                GameObjectName = go.name,
                Overwritten    = existed
            });
        }
    }
}

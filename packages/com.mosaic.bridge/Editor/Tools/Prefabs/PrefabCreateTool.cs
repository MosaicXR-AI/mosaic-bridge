using System.IO;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Prefabs
{
    public static class PrefabCreateTool
    {
        [MosaicTool("prefab/create",
                    "Saves a scene GameObject as a prefab asset at the specified project path",
                    isReadOnly: false)]
        public static ToolResult<PrefabCreateResult> Execute(PrefabCreateParams p)
        {
            if (string.IsNullOrEmpty(p.GameObjectName))
                return ToolResult<PrefabCreateResult>.Fail(
                    "GameObjectName is required", ErrorCodes.INVALID_PARAM);
            if (string.IsNullOrEmpty(p.PrefabPath))
                return ToolResult<PrefabCreateResult>.Fail(
                    "PrefabPath is required", ErrorCodes.INVALID_PARAM);

            var go = GameObject.Find(p.GameObjectName);
            if (go == null)
                return ToolResult<PrefabCreateResult>.Fail(
                    $"GameObject '{p.GameObjectName}' not found", ErrorCodes.NOT_FOUND);

            bool existed = AssetDatabase.AssetPathExists(p.PrefabPath);
            if (existed && !p.OverwriteExisting)
                return ToolResult<PrefabCreateResult>.Fail(
                    $"Prefab already exists at '{p.PrefabPath}'. Set OverwriteExisting=true to replace it.",
                    ErrorCodes.CONFLICT);

            var absoluteDir = Path.GetDirectoryName(
                Path.Combine(Application.dataPath, "..", p.PrefabPath));
            if (!string.IsNullOrEmpty(absoluteDir))
                Directory.CreateDirectory(absoluteDir);

            var prefabAsset = PrefabUtility.SaveAsPrefabAsset(go, p.PrefabPath);
            if (prefabAsset == null)
                return ToolResult<PrefabCreateResult>.Fail(
                    $"PrefabUtility.SaveAsPrefabAsset returned null for path '{p.PrefabPath}'",
                    ErrorCodes.INTERNAL_ERROR);

            return ToolResult<PrefabCreateResult>.Ok(new PrefabCreateResult
            {
                PrefabPath     = p.PrefabPath,
                GameObjectName = go.name,
                Overwritten    = existed
            });
        }
    }
}

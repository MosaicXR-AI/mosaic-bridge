using System.IO;
using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Assets
{
    public static class AssetInfoTool
    {
        [MosaicTool("asset/info",
                    "Returns metadata for a project asset at the given path",
                    isReadOnly: true)]
        public static ToolResult<AssetInfoResult> Execute(AssetInfoParams p)
        {
            if (string.IsNullOrEmpty(p.Path))
                return ToolResult<AssetInfoResult>.Fail(
                    "Path is required", ErrorCodes.INVALID_PARAM);

            var guid = AssetDatabase.AssetPathToGUID(p.Path);
            if (string.IsNullOrEmpty(guid))
                return ToolResult<AssetInfoResult>.Fail(
                    $"Asset not found at path '{p.Path}'", ErrorCodes.NOT_FOUND);

            var assetType = AssetDatabase.GetMainAssetTypeAtPath(p.Path);
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(p.Path);
            var labels    = mainAsset != null
                ? AssetDatabase.GetLabels(mainAsset)
                : new string[0];

            long fileSize = -1;
            try
            {
                var absolutePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", p.Path));
                fileSize = new FileInfo(absolutePath).Length;
            }
            catch { /* leave -1 if inaccessible */ }

            bool isPrefab = assetType == typeof(GameObject)
                            && p.Path.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase);

            return ToolResult<AssetInfoResult>.Ok(new AssetInfoResult
            {
                Path         = p.Path,
                Name         = Path.GetFileName(p.Path),
                Type         = assetType?.Name ?? "Unknown",
                FullTypeName = assetType?.FullName ?? "Unknown",
                Guid         = guid,
                FileSize     = fileSize,
                Labels       = labels,
                IsPrefab     = isPrefab
            });
        }
    }
}

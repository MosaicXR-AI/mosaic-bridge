using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Assets
{
    public static class AssetImportTool
    {
        [MosaicTool("asset/import",
                    "Force-reimports an asset at the given Assets-relative path")]
        public static ToolResult<AssetImportResult> Execute(AssetImportParams p)
        {
            if (!p.Path.StartsWith("Assets/"))
                return ToolResult<AssetImportResult>.Fail(
                    $"Path must start with 'Assets/' — got '{p.Path}'", ErrorCodes.INVALID_PARAM);

            AssetDatabase.ImportAsset(p.Path, ImportAssetOptions.ForceUpdate);

            var typeName = AssetDatabase.GetMainAssetTypeAtPath(p.Path)?.Name ?? "Unknown";
            return ToolResult<AssetImportResult>.Ok(new AssetImportResult
            {
                Path = p.Path,
                Type = typeName
            });
        }
    }
}

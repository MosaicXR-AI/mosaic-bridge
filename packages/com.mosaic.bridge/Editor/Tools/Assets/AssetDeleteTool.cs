using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Assets
{
    public static class AssetDeleteTool
    {
        [MosaicTool("asset/delete",
                    "Deletes an asset from the project by its Assets-relative path")]
        public static ToolResult<AssetDeleteResult> Execute(AssetDeleteParams p)
        {
            if (!p.Path.StartsWith("Assets/"))
                return ToolResult<AssetDeleteResult>.Fail(
                    $"Path must start with 'Assets/' — got '{p.Path}'", ErrorCodes.INVALID_PARAM);

            if (AssetDatabase.LoadMainAssetAtPath(p.Path) == null)
                return ToolResult<AssetDeleteResult>.Fail(
                    $"Asset not found at path '{p.Path}'", ErrorCodes.NOT_FOUND);

            bool ok = AssetDatabase.DeleteAsset(p.Path);
            if (!ok)
                return ToolResult<AssetDeleteResult>.Fail(
                    "AssetDatabase.DeleteAsset returned false", ErrorCodes.INTERNAL_ERROR);

            return ToolResult<AssetDeleteResult>.Ok(new AssetDeleteResult
            {
                Path    = p.Path,
                Deleted = true
            });
        }
    }
}

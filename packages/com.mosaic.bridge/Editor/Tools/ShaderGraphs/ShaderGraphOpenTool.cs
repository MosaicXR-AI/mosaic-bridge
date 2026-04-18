using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.ShaderGraphs
{
    public static class ShaderGraphOpenTool
    {
        [MosaicTool("shadergraph/open",
                    "Opens a ShaderGraph asset in the ShaderGraph editor window",
                    isReadOnly: false)]
        public static ToolResult<ShaderGraphOpenResult> Execute(ShaderGraphOpenParams p)
        {
            if (string.IsNullOrEmpty(p.AssetPath))
                return ToolResult<ShaderGraphOpenResult>.Fail(
                    "AssetPath is required", ErrorCodes.INVALID_PARAM);

            if (!p.AssetPath.EndsWith(".shadergraph"))
                return ToolResult<ShaderGraphOpenResult>.Fail(
                    $"Path '{p.AssetPath}' is not a .shadergraph file", ErrorCodes.INVALID_PARAM);

            if (!AssetDatabase.AssetPathExists(p.AssetPath))
                return ToolResult<ShaderGraphOpenResult>.Fail(
                    $"ShaderGraph not found at '{p.AssetPath}'", ErrorCodes.NOT_FOUND);

            var asset = AssetDatabase.LoadMainAssetAtPath(p.AssetPath);
            if (asset == null)
                return ToolResult<ShaderGraphOpenResult>.Fail(
                    $"Failed to load asset at '{p.AssetPath}'", ErrorCodes.INTERNAL_ERROR);

            bool opened = AssetDatabase.OpenAsset(asset);

            return ToolResult<ShaderGraphOpenResult>.Ok(new ShaderGraphOpenResult
            {
                AssetPath = p.AssetPath,
                Opened    = opened
            });
        }
    }
}

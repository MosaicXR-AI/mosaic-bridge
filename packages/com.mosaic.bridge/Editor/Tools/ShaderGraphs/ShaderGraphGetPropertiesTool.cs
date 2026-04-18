using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.ShaderGraphs
{
    public static class ShaderGraphGetPropertiesTool
    {
        [MosaicTool("shadergraph/get-properties",
                    "Returns the exposed properties of a ShaderGraph asset",
                    isReadOnly: true)]
        public static ToolResult<ShaderGraphGetPropertiesResult> Execute(ShaderGraphGetPropertiesParams p)
        {
            if (string.IsNullOrEmpty(p.AssetPath))
                return ToolResult<ShaderGraphGetPropertiesResult>.Fail(
                    "AssetPath is required", ErrorCodes.INVALID_PARAM);

            if (!p.AssetPath.EndsWith(".shadergraph"))
                return ToolResult<ShaderGraphGetPropertiesResult>.Fail(
                    $"Path '{p.AssetPath}' is not a .shadergraph file", ErrorCodes.INVALID_PARAM);

            if (!AssetDatabase.AssetPathExists(p.AssetPath))
                return ToolResult<ShaderGraphGetPropertiesResult>.Fail(
                    $"ShaderGraph not found at '{p.AssetPath}'", ErrorCodes.NOT_FOUND);

            var graph = ShaderGraphJsonHelper.ReadGraph(p.AssetPath);
            if (graph == null)
                return ToolResult<ShaderGraphGetPropertiesResult>.Fail(
                    $"Failed to read ShaderGraph JSON at '{p.AssetPath}'", ErrorCodes.INTERNAL_ERROR);

            var properties = ShaderGraphJsonHelper.ExtractProperties(graph);

            return ToolResult<ShaderGraphGetPropertiesResult>.Ok(new ShaderGraphGetPropertiesResult
            {
                AssetPath  = p.AssetPath,
                Properties = properties,
                Count      = properties.Count
            });
        }
    }
}

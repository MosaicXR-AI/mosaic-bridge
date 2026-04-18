using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.ShaderGraphs
{
    public static class ShaderGraphSetPropertyDefaultTool
    {
        [MosaicTool("shadergraph/set-property-default",
                    "Sets the default value of an exposed property in a ShaderGraph asset",
                    isReadOnly: false)]
        public static ToolResult<ShaderGraphSetPropertyDefaultResult> Execute(ShaderGraphSetPropertyDefaultParams p)
        {
            if (string.IsNullOrEmpty(p.AssetPath))
                return ToolResult<ShaderGraphSetPropertyDefaultResult>.Fail(
                    "AssetPath is required", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrEmpty(p.PropertyName))
                return ToolResult<ShaderGraphSetPropertyDefaultResult>.Fail(
                    "PropertyName is required", ErrorCodes.INVALID_PARAM);

            if (string.IsNullOrEmpty(p.Value))
                return ToolResult<ShaderGraphSetPropertyDefaultResult>.Fail(
                    "Value is required", ErrorCodes.INVALID_PARAM);

            if (!p.AssetPath.EndsWith(".shadergraph"))
                return ToolResult<ShaderGraphSetPropertyDefaultResult>.Fail(
                    $"Path '{p.AssetPath}' is not a .shadergraph file", ErrorCodes.INVALID_PARAM);

            if (!AssetDatabase.AssetPathExists(p.AssetPath))
                return ToolResult<ShaderGraphSetPropertyDefaultResult>.Fail(
                    $"ShaderGraph not found at '{p.AssetPath}'", ErrorCodes.NOT_FOUND);

            var graph = ShaderGraphJsonHelper.ReadGraph(p.AssetPath);
            if (graph == null)
                return ToolResult<ShaderGraphSetPropertyDefaultResult>.Fail(
                    $"Failed to read ShaderGraph JSON at '{p.AssetPath}'", ErrorCodes.INTERNAL_ERROR);

            string oldValue = ShaderGraphJsonHelper.SetPropertyDefault(graph, p.PropertyName, p.Value);

            if (oldValue == null)
                return ToolResult<ShaderGraphSetPropertyDefaultResult>.Fail(
                    $"Property '{p.PropertyName}' not found in ShaderGraph at '{p.AssetPath}'",
                    ErrorCodes.NOT_FOUND);

            // Write the modified graph back to disk
            ShaderGraphJsonHelper.WriteGraph(p.AssetPath, graph);
            AssetDatabase.ImportAsset(p.AssetPath, ImportAssetOptions.ForceUpdate);

            return ToolResult<ShaderGraphSetPropertyDefaultResult>.Ok(new ShaderGraphSetPropertyDefaultResult
            {
                AssetPath    = p.AssetPath,
                PropertyName = p.PropertyName,
                OldValue     = oldValue,
                NewValue     = p.Value
            });
        }
    }
}

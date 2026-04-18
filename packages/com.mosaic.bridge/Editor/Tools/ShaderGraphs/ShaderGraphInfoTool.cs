using System.IO;
using System.Linq;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.ShaderGraphs
{
    public static class ShaderGraphInfoTool
    {
        [MosaicTool("shadergraph/info",
                    "Returns information about a ShaderGraph asset by parsing its JSON file",
                    isReadOnly: true)]
        public static ToolResult<ShaderGraphInfoResult> Execute(ShaderGraphInfoParams p)
        {
            if (string.IsNullOrEmpty(p.AssetPath))
                return ToolResult<ShaderGraphInfoResult>.Fail(
                    "AssetPath is required", ErrorCodes.INVALID_PARAM);

            if (!p.AssetPath.EndsWith(".shadergraph"))
                return ToolResult<ShaderGraphInfoResult>.Fail(
                    $"Path '{p.AssetPath}' is not a .shadergraph file", ErrorCodes.INVALID_PARAM);

            if (!AssetDatabase.AssetPathExists(p.AssetPath))
                return ToolResult<ShaderGraphInfoResult>.Fail(
                    $"ShaderGraph not found at '{p.AssetPath}'", ErrorCodes.NOT_FOUND);

            var graph = ShaderGraphJsonHelper.ReadGraph(p.AssetPath);
            if (graph == null)
                return ToolResult<ShaderGraphInfoResult>.Fail(
                    $"Failed to read ShaderGraph JSON at '{p.AssetPath}'", ErrorCodes.INTERNAL_ERROR);

            var properties = ShaderGraphJsonHelper.ExtractProperties(graph);
            var guid = AssetDatabase.AssetPathToGUID(p.AssetPath);

            return ToolResult<ShaderGraphInfoResult>.Ok(new ShaderGraphInfoResult
            {
                AssetPath     = p.AssetPath,
                Name          = Path.GetFileNameWithoutExtension(p.AssetPath),
                Guid          = guid,
                NodeCount     = ShaderGraphJsonHelper.CountNodes(graph),
                EdgeCount     = ShaderGraphJsonHelper.CountEdges(graph),
                PropertyCount = properties.Count,
                PropertyNames = properties.Select(prop => prop.Name).ToList()
            });
        }
    }
}

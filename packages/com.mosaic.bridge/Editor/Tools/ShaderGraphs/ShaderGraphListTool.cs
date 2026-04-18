using System.Collections.Generic;
using System.IO;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Tools.Shared;

namespace Mosaic.Bridge.Tools.ShaderGraphs
{
    public static class ShaderGraphListTool
    {
        [MosaicTool("shadergraph/list",
                    "Lists all ShaderGraph assets in the project",
                    isReadOnly: true)]
        public static ToolResult<ShaderGraphListResult> Execute(ShaderGraphListParams p)
        {
            // Find all Shader assets, then filter to .shadergraph extension
            var guids = AssetDatabase.FindAssets("t:Shader", new[] { p.Path ?? "Assets" });
            var allGraphs = new List<ShaderGraphRef>();

            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (!assetPath.EndsWith(".shadergraph"))
                    continue;

                allGraphs.Add(new ShaderGraphRef
                {
                    Path = assetPath,
                    Name = Path.GetFileNameWithoutExtension(assetPath),
                    Guid = guid
                });
            }

            int totalCount = allGraphs.Count;
            int offset = PaginationHelper.DecodeOffset(p.PageToken);
            var (page, nextToken) = PaginationHelper.Paginate(allGraphs, offset, p.PageSize);

            return ToolResult<ShaderGraphListResult>.Ok(new ShaderGraphListResult
            {
                Graphs        = page,
                Count         = page.Count,
                TotalCount    = totalCount,
                Truncated     = nextToken != null,
                NextPageToken = nextToken
            });
        }
    }
}

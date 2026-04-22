using System.Collections.Generic;
using System.IO;
using UnityEngine;
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
            // Use file system search to reliably find .shadergraph files regardless of AssetDatabase indexing state.
            string searchRoot = Application.dataPath;
            if (p.Path != null && p.Path != "Assets")
            {
                searchRoot = Path.GetFullPath(
                    Path.Combine(Application.dataPath, "..", p.Path));
            }

            var allGraphs = new List<ShaderGraphRef>();

            if (Directory.Exists(searchRoot))
            {
                var files = Directory.GetFiles(searchRoot, "*.shadergraph", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    // Convert absolute path to asset-relative path
                    string normalized = file.Replace('\\', '/');
                    string dataPath = Application.dataPath.Replace('\\', '/');
                    if (!normalized.StartsWith(dataPath)) continue;

                    string assetPath = "Assets" + normalized.Substring(dataPath.Length);
                    string guid = AssetDatabase.AssetPathToGUID(assetPath);

                    allGraphs.Add(new ShaderGraphRef
                    {
                        Path = assetPath,
                        Name = Path.GetFileNameWithoutExtension(assetPath),
                        Guid = string.IsNullOrEmpty(guid) ? "" : guid
                    });
                }
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

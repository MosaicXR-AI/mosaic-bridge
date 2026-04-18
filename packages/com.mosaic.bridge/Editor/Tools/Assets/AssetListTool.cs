using System.Collections.Generic;
using System.IO;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Core.Dispatcher;
using Mosaic.Bridge.Tools.Shared;

namespace Mosaic.Bridge.Tools.Assets
{
    public static class AssetListTool
    {
        [MosaicTool("asset/list",
                    "Lists project assets matching an optional AssetDatabase filter string",
                    isReadOnly: true)]
        public static ToolResult<AssetListResult> Execute(AssetListParams p)
        {
            var guids = AssetDatabase.FindAssets(p.Filter ?? "", new[] { p.Path ?? "Assets" });
            int totalCount = guids.Length;

            // Build full list of AssetInfo for all matching GUIDs
            var allAssets = new List<AssetInfo>(totalCount);
            for (int i = 0; i < totalCount; i++)
            {
                // Story 2.11: Allow cancellation during potentially large asset scans
                ToolExecutionContext.CancellationToken.ThrowIfCancellationRequested();

                var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                var typeName  = AssetDatabase.GetMainAssetTypeAtPath(assetPath)?.Name ?? "Unknown";
                allAssets.Add(new AssetInfo
                {
                    Path = assetPath,
                    Name = Path.GetFileName(assetPath),
                    Type = typeName
                });
            }

            int offset = PaginationHelper.DecodeOffset(p.PageToken);
            var (page, nextToken) = PaginationHelper.Paginate(allAssets, offset, p.PageSize);

            return ToolResult<AssetListResult>.Ok(new AssetListResult
            {
                Assets        = page,
                Count         = page.Count,
                TotalCount    = totalCount,
                Truncated     = nextToken != null,
                NextPageToken = nextToken
            });
        }
    }
}

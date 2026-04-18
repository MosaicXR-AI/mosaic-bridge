using System.Collections.Generic;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Tools.Shared;

namespace Mosaic.Bridge.Tools.Search
{
    public static class SearchByLayerTool
    {
        [MosaicTool("search/by_layer",
                    "Finds all scene GameObjects assigned to the given layer",
                    isReadOnly: true)]
        public static ToolResult<SearchByLayerResult> Execute(SearchByLayerParams p)
        {
            int layer = LayerMask.NameToLayer(p.LayerName);
            if (layer == -1)
                return ToolResult<SearchByLayerResult>.Fail(
                    $"Layer '{p.LayerName}' does not exist", ErrorCodes.INVALID_PARAM);

            var all        = Resources.FindObjectsOfTypeAll<GameObject>();
            var allMatches = new List<SearchResult>();

            foreach (var go in all)
            {
                if (go.layer == layer && go.scene.IsValid())
                {
                    allMatches.Add(new SearchResult
                    {
                        InstanceId    = go.GetInstanceID(),
                        Name          = go.name,
                        HierarchyPath = GetHierarchyPath(go.transform)
                    });
                }
            }

            int totalCount = allMatches.Count;
            int offset = PaginationHelper.DecodeOffset(p.PageToken);
            var (page, nextToken) = PaginationHelper.Paginate(allMatches, offset, p.PageSize);

            return ToolResult<SearchByLayerResult>.Ok(new SearchByLayerResult
            {
                Matches       = page,
                Count         = page.Count,
                TotalCount    = totalCount,
                NextPageToken = nextToken
            });
        }

        private static string GetHierarchyPath(Transform t)
        {
            var path    = t.name;
            var current = t.parent;
            while (current != null)
            {
                path    = current.name + "/" + path;
                current = current.parent;
            }
            return path;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Tools.Shared;

namespace Mosaic.Bridge.Tools.Search
{
    public static class SearchByTagTool
    {
        [MosaicTool("search/by_tag",
                    "Finds all GameObjects in the scene that have the given tag",
                    isReadOnly: true)]
        public static ToolResult<SearchByTagResult> Execute(SearchByTagParams p)
        {
            GameObject[] objects;
            try
            {
                objects = GameObject.FindGameObjectsWithTag(p.Tag);
            }
            catch (UnityException ex)
            {
                return ToolResult<SearchByTagResult>.Fail(
                    $"Invalid tag '{p.Tag}': {ex.Message}", ErrorCodes.INVALID_PARAM);
            }

            var allMatches = new List<SearchResult>(objects.Length);
            foreach (var go in objects)
            {
                allMatches.Add(new SearchResult
                {
                    InstanceId    = go.GetInstanceID(),
                    Name          = go.name,
                    HierarchyPath = GetHierarchyPath(go.transform)
                });
            }

            int totalCount = allMatches.Count;
            int offset = PaginationHelper.DecodeOffset(p.PageToken);
            var (page, nextToken) = PaginationHelper.Paginate(allMatches, offset, p.PageSize);

            return ToolResult<SearchByTagResult>.Ok(new SearchByTagResult
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

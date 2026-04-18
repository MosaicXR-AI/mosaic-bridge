using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Core.Dispatcher;
using Mosaic.Bridge.Tools.Shared;

namespace Mosaic.Bridge.Tools.Search
{
    public static class SearchByNameTool
    {
        [MosaicTool("search/by_name",
                    "Finds scene GameObjects whose names match a substring or regex pattern",
                    isReadOnly: true)]
        public static ToolResult<SearchByNameResult> Execute(SearchByNameParams p)
        {
            if (string.IsNullOrEmpty(p.Pattern))
                return ToolResult<SearchByNameResult>.Fail(
                    "Pattern is required", ErrorCodes.INVALID_PARAM);

            Regex regex = null;
            if (p.UseRegex)
            {
                try
                {
                    regex = new Regex(p.Pattern, RegexOptions.IgnoreCase);
                }
                catch (ArgumentException ex)
                {
                    return ToolResult<SearchByNameResult>.Fail(
                        $"Invalid regex pattern: {ex.Message}", ErrorCodes.INVALID_PARAM);
                }
            }

            var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            var allMatches = new List<SearchResult>();

            foreach (var go in allObjects)
            {
                // Story 2.11: Allow cancellation during potentially large scene scans
                ToolExecutionContext.CancellationToken.ThrowIfCancellationRequested();

                // Only include objects in a valid scene (exclude assets/prefabs in memory)
                if (!go.scene.IsValid()) continue;

                // Inactive filter
                if (!p.IncludeInactive && !go.activeInHierarchy) continue;

                bool isMatch = p.UseRegex
                    ? regex.IsMatch(go.name)
                    : go.name.IndexOf(p.Pattern, StringComparison.OrdinalIgnoreCase) >= 0;

                if (!isMatch) continue;

                allMatches.Add(new SearchResult
                {
                    InstanceId    = go.GetInstanceID(),
                    Name          = go.name,
                    HierarchyPath = GetHierarchyPath(go.transform)
                });
            }

            int totalFound = allMatches.Count;
            int offset = PaginationHelper.DecodeOffset(p.PageToken);
            var (page, nextToken) = PaginationHelper.Paginate(allMatches, offset, p.PageSize);

            return ToolResult<SearchByNameResult>.Ok(new SearchByNameResult
            {
                Matches       = page,
                TotalFound    = totalFound,
                Truncated     = nextToken != null,
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

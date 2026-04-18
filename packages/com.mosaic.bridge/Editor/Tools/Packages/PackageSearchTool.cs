using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Packages
{
    public static class PackageSearchTool
    {
        [MosaicTool("package/search",
                    "Searches the Unity package registry for packages matching a query",
                    isReadOnly: true)]
        public static ToolResult<PackageSearchResult> Execute(PackageSearchParams p)
        {
            if (string.IsNullOrWhiteSpace(p.Query))
                return ToolResult<PackageSearchResult>.Fail(
                    "Query is required", ErrorCodes.INVALID_PARAM);

            SearchRequest request = Client.Search(p.Query);

            if (!PackageListTool.WaitForCompletion(request))
                return ToolResult<PackageSearchResult>.Fail(
                    $"Package search request timed out after 30 seconds for '{p.Query}'",
                    ErrorCodes.INTERNAL_ERROR);

            if (request.Status == StatusCode.Failure)
                return ToolResult<PackageSearchResult>.Fail(
                    $"Package search failed for '{p.Query}': {request.Error?.message ?? "Unknown error"}",
                    ErrorCodes.INTERNAL_ERROR);

            var packages = new List<PackageRef>();
            foreach (var info in request.Result)
            {
                packages.Add(PackageListTool.ToPackageRef(info));
            }

            return ToolResult<PackageSearchResult>.Ok(new PackageSearchResult
            {
                Packages = packages,
                Count    = packages.Count,
                Query    = p.Query
            });
        }
    }
}

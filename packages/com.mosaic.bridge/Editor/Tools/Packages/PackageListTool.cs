using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Packages
{
    public static class PackageListTool
    {
        private const int TimeoutMs = 30_000;
        private const int PollIntervalMs = 10;

        [MosaicTool("package/list",
                    "Lists all packages installed in the current Unity project",
                    isReadOnly: true)]
        public static ToolResult<PackageListResult> Execute(PackageListParams p)
        {
            ListRequest request = Client.List(p.OfflineMode);

            if (!WaitForCompletion(request))
                return ToolResult<PackageListResult>.Fail(
                    "Package list request timed out after 30 seconds",
                    ErrorCodes.INTERNAL_ERROR);

            if (request.Status == StatusCode.Failure)
                return ToolResult<PackageListResult>.Fail(
                    $"Package list failed: {request.Error?.message ?? "Unknown error"}",
                    ErrorCodes.INTERNAL_ERROR);

            var packages = new List<PackageRef>();
            foreach (var info in request.Result)
            {
                packages.Add(ToPackageRef(info));
            }

            return ToolResult<PackageListResult>.Ok(new PackageListResult
            {
                Packages = packages,
                Count    = packages.Count
            });
        }

        internal static PackageRef ToPackageRef(UnityEditor.PackageManager.PackageInfo info)
        {
            return new PackageRef
            {
                Name        = info.name,
                Version     = info.version,
                DisplayName = info.displayName,
                Description = info.description,
                Source      = info.source.ToString().ToLowerInvariant()
            };
        }

        internal static bool WaitForCompletion(Request request)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(TimeoutMs);
            while (!request.IsCompleted)
            {
                if (DateTime.UtcNow > deadline)
                    return false;
                Thread.Sleep(PollIntervalMs);
            }
            return true;
        }
    }
}

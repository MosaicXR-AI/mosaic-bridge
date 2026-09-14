using Newtonsoft.Json.Linq;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Core.Jobs;

namespace Mosaic.Bridge.Tools.Packages
{
    public static class PackageRemoveTool
    {
        [MosaicTool("package/remove",
                    "Removes an installed Unity package by name. Starts the removal and returns immediately with " +
                    "a JobId — this never blocks, same reasoning as package/add (L15). Poll job/status with the " +
                    "JobId for the real outcome.",
                    isReadOnly: false)]
        public static ToolResult<PackageRemoveResult> Execute(PackageRemoveParams p)
        {
            if (string.IsNullOrWhiteSpace(p?.Name))
                return ToolResult<PackageRemoveResult>.Fail(
                    "Name is required", ErrorCodes.INVALID_PARAM);

            var record = JobRegistry.Start("package/remove", new JObject { ["Name"] = p.Name });

            if (record.Status == JobStatus.Failed)
                return ToolResult<PackageRemoveResult>.Fail(record.Message, ErrorCodes.INTERNAL_ERROR);

            return ToolResult<PackageRemoveResult>.Ok(new PackageRemoveResult
            {
                JobId = record.JobId,
                Status = "pending",
                Message = record.Message ?? $"Removing '{p.Name}'… poll job/status with this JobId.",
            });
        }
    }
}

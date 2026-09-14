using Newtonsoft.Json.Linq;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Core.Jobs;

namespace Mosaic.Bridge.Tools.Packages
{
    public static class PackageAddTool
    {
        [MosaicTool("package/add",
                    "Installs or updates a Unity package by name, version, or git URL. Starts the install and " +
                    "returns immediately with a JobId — this never blocks (a package install commonly triggers " +
                    "a domain reload, and this route used to Thread.Sleep on the main thread waiting for one, " +
                    "which could lose its own response outright). Poll job/status with the JobId for the real " +
                    "outcome.",
                    isReadOnly: false)]
        public static ToolResult<PackageAddResult> Execute(PackageAddParams p)
        {
            if (string.IsNullOrWhiteSpace(p?.Identifier))
                return ToolResult<PackageAddResult>.Fail(
                    "Identifier is required", ErrorCodes.INVALID_PARAM);

            var record = JobRegistry.Start("package/add", new JObject { ["Identifier"] = p.Identifier });

            if (record.Status == JobStatus.Failed)
                return ToolResult<PackageAddResult>.Fail(record.Message, ErrorCodes.INTERNAL_ERROR);

            return ToolResult<PackageAddResult>.Ok(new PackageAddResult
            {
                JobId = record.JobId,
                Status = "pending",
                Message = record.Message ?? $"Installing '{p.Identifier}'… poll job/status with this JobId.",
            });
        }
    }
}

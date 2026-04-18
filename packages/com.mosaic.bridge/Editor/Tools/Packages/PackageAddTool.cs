using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Packages
{
    public static class PackageAddTool
    {
        [MosaicTool("package/add",
                    "Installs or updates a Unity package by name, version, or git URL",
                    isReadOnly: false)]
        public static ToolResult<PackageAddResult> Execute(PackageAddParams p)
        {
            if (string.IsNullOrWhiteSpace(p.Identifier))
                return ToolResult<PackageAddResult>.Fail(
                    "Identifier is required", ErrorCodes.INVALID_PARAM);

            AddRequest request = Client.Add(p.Identifier);

            if (!PackageListTool.WaitForCompletion(request))
                return ToolResult<PackageAddResult>.Fail(
                    $"Package add request timed out after 30 seconds for '{p.Identifier}'",
                    ErrorCodes.INTERNAL_ERROR);

            if (request.Status == StatusCode.Failure)
                return ToolResult<PackageAddResult>.Fail(
                    $"Failed to add package '{p.Identifier}': {request.Error?.message ?? "Unknown error"}",
                    ErrorCodes.INTERNAL_ERROR);

            return ToolResult<PackageAddResult>.Ok(new PackageAddResult
            {
                Package = PackageListTool.ToPackageRef(request.Result),
                Message = $"Successfully installed {request.Result.displayName} ({request.Result.name}@{request.Result.version})"
            });
        }
    }
}

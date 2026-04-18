using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Packages
{
    public static class PackageRemoveTool
    {
        [MosaicTool("package/remove",
                    "Removes an installed Unity package by name",
                    isReadOnly: false)]
        public static ToolResult<PackageRemoveResult> Execute(PackageRemoveParams p)
        {
            if (string.IsNullOrWhiteSpace(p.Name))
                return ToolResult<PackageRemoveResult>.Fail(
                    "Name is required", ErrorCodes.INVALID_PARAM);

            RemoveRequest request = Client.Remove(p.Name);

            if (!PackageListTool.WaitForCompletion(request))
                return ToolResult<PackageRemoveResult>.Fail(
                    $"Package remove request timed out after 30 seconds for '{p.Name}'",
                    ErrorCodes.INTERNAL_ERROR);

            if (request.Status == StatusCode.Failure)
                return ToolResult<PackageRemoveResult>.Fail(
                    $"Failed to remove package '{p.Name}': {request.Error?.message ?? "Unknown error"}",
                    ErrorCodes.INTERNAL_ERROR);

            return ToolResult<PackageRemoveResult>.Ok(new PackageRemoveResult
            {
                Name    = p.Name,
                Removed = true,
                Message = $"Successfully removed {p.Name}"
            });
        }
    }
}

using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.EditorOps
{
    public static class EditorExecuteMenuItemTool
    {
        [MosaicTool("editor/execute-menu-item",
                    "Executes a Unity Editor menu item by path (e.g. 'GameObject/3D Object/Cube')",
                    isReadOnly: false)]
        public static ToolResult<EditorExecuteMenuItemResult> Execute(EditorExecuteMenuItemParams p)
        {
            bool success = EditorApplication.ExecuteMenuItem(p.MenuPath);

            if (!success)
            {
                return ToolResult<EditorExecuteMenuItemResult>.Fail(
                    $"Menu item '{p.MenuPath}' not found or could not be executed. " +
                    "Ensure the path matches exactly (case-sensitive), e.g. 'GameObject/3D Object/Cube'.",
                    ErrorCodes.NOT_FOUND);
            }

            return ToolResult<EditorExecuteMenuItemResult>.Ok(new EditorExecuteMenuItemResult
            {
                MenuPath = p.MenuPath,
                Executed = true
            });
        }
    }
}

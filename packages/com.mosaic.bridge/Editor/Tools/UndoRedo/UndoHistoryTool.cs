using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.UndoRedo
{
    public static class UndoHistoryTool
    {
        [MosaicTool("undo/history",
                    "Returns the current undo/redo state: the next action available for undo and redo",
                    isReadOnly: true)]
        public static ToolResult<UndoHistoryResult> History(UndoHistoryParams p)
        {
            // Unity 6 public API only exposes the current recording group name, not the full undo/redo stack
            string currentGroup = UnityEditor.Undo.GetCurrentGroupName();

            return ToolResult<UndoHistoryResult>.Ok(new UndoHistoryResult
            {
                CurrentUndoAction = currentGroup,
                CurrentRedoAction = "",
                CanUndo = !string.IsNullOrEmpty(currentGroup),
                CanRedo = false
            });
        }
    }
}

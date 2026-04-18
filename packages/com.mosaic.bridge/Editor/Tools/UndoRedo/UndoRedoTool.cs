using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.UndoRedo
{
    public static class UndoRedoTool
    {
        [MosaicTool("undo/redo",
                    "Performs a redo operation in the Unity Editor, reapplying the last undone action",
                    isReadOnly: false)]
        public static ToolResult<UndoRedoResult> Redo(UndoRedoParams p)
        {
            string name = UnityEditor.Undo.GetCurrentGroupName();
            UnityEditor.Undo.PerformRedo();

            return ToolResult<UndoRedoResult>.Ok(new UndoRedoResult
            {
                Performed = true,
                ActionName = name
            });
        }
    }
}

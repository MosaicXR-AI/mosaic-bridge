using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;

namespace Mosaic.Bridge.Tools.UndoRedo
{
    public static class UndoUndoTool
    {
        [MosaicTool("undo/undo",
                    "Performs an undo operation in the Unity Editor, reversing the last recorded action",
                    isReadOnly: false)]
        public static ToolResult<UndoUndoResult> Undo(UndoUndoParams p)
        {
            // Undo.undoName/redoName are internal in Unity 6; GetCurrentGroupName() is the public proxy
            string name = UnityEditor.Undo.GetCurrentGroupName();
            UnityEditor.Undo.PerformUndo();

            return ToolResult<UndoUndoResult>.Ok(new UndoUndoResult
            {
                Performed = true,
                ActionName = name
            });
        }
    }
}

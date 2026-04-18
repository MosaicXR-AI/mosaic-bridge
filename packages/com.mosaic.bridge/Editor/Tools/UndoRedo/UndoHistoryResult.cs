namespace Mosaic.Bridge.Tools.UndoRedo
{
    public sealed class UndoHistoryResult
    {
        public string CurrentUndoAction { get; set; }
        public string CurrentRedoAction { get; set; }
        public bool CanUndo { get; set; }
        public bool CanRedo { get; set; }
    }
}

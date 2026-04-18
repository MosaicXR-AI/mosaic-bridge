namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenWfcResult
    {
        public int        Width          { get; set; }
        public int        Height         { get; set; }
        public int        Depth          { get; set; }
        public string[][] Grid           { get; set; }
        public bool       Success        { get; set; }
        public int        Backtracks     { get; set; }
        public string[]   GameObjectNames { get; set; }
        public int        CollapsedCells { get; set; }
        public int        TotalCells     { get; set; }
    }
}

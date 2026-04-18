namespace Mosaic.Bridge.Tools.AdvancedNavigation
{
    public sealed class NavBehaviorTreeResult
    {
        public string   ScriptDirectory { get; set; }
        public string   TreeName        { get; set; }
        public int      NodeCount       { get; set; }
        public string[] NodeTypes       { get; set; }
    }
}

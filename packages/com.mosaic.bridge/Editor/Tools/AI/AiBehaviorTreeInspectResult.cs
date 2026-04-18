namespace Mosaic.Bridge.Tools.AI
{
    public sealed class AiBehaviorTreeInspectResult
    {
        public string   TreeStructure  { get; set; }
        public int      NodeCount      { get; set; }
        public string[] BlackboardKeys { get; set; }
    }
}

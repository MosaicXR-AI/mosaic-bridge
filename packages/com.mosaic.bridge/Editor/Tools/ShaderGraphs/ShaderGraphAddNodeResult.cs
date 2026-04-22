namespace Mosaic.Bridge.Tools.ShaderGraphs
{
    public sealed class ShaderGraphAddNodeResult
    {
        public string GraphPath    { get; set; }
        public string NodeId       { get; set; }  // GUID assigned to this node
        public string NodeType     { get; set; }  // fully-qualified type name
        public string NodeName     { get; set; }  // display name in graph
        public int    TotalNodes   { get; set; }
        public ShaderGraphNodeSlot[] Slots { get; set; }
    }

    public sealed class ShaderGraphNodeSlot
    {
        public int    Id          { get; set; }
        public string DisplayName { get; set; }
        public string Direction   { get; set; }  // "Input" or "Output"
    }
}

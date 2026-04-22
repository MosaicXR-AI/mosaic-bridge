namespace Mosaic.Bridge.Tools.ShaderGraphs
{
    public sealed class ShaderGraphConnectResult
    {
        public string GraphPath    { get; set; }
        public string OutputNodeId { get; set; }
        public int    OutputSlotId { get; set; }
        public string InputNodeId  { get; set; }
        public int    InputSlotId  { get; set; }
        public int    TotalEdges   { get; set; }
    }
}

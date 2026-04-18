namespace Mosaic.Bridge.Tools.DataViz
{
    /// <summary>Result envelope for chart/network.</summary>
    public sealed class ChartNetworkResult
    {
        public string GameObjectName { get; set; }
        public int    InstanceId     { get; set; }
        public int    NodeCount      { get; set; }
        public int    EdgeCount      { get; set; }
    }
}

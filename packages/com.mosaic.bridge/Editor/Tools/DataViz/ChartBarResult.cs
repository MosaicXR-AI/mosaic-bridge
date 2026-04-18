namespace Mosaic.Bridge.Tools.DataViz
{
    /// <summary>Result envelope for chart/bar.</summary>
    public sealed class ChartBarResult
    {
        public string GameObjectName { get; set; }
        public int    InstanceId     { get; set; }
        public int    BarCount       { get; set; }
    }
}

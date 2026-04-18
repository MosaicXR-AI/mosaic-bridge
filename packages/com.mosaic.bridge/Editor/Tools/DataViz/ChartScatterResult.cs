namespace Mosaic.Bridge.Tools.DataViz
{
    /// <summary>Result envelope for chart/scatter.</summary>
    public sealed class ChartScatterResult
    {
        public string GameObjectName { get; set; }
        public int    InstanceId     { get; set; }
        public int    PointCount     { get; set; }
    }
}

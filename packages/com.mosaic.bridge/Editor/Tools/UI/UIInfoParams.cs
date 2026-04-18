namespace Mosaic.Bridge.Tools.UI
{
    public sealed class UIInfoParams
    {
        /// <summary>InstanceId of a specific Canvas or UI element to query. If omitted, returns all Canvases.</summary>
        public int? InstanceId { get; set; }

        /// <summary>Name of a specific Canvas or UI element to query (fallback if InstanceId not set).</summary>
        public string Name { get; set; }
    }
}

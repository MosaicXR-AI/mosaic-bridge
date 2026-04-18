namespace Mosaic.Bridge.Tools.UI
{
    public sealed class UISetPropertiesResult
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string[] ModifiedProperties { get; set; }
        public string DetectedComponentType { get; set; }
    }
}

namespace Mosaic.Bridge.Tools.UI
{
    public sealed class UIAddElementResult
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string HierarchyPath { get; set; }
        public string ElementType { get; set; }
        public string[] Components { get; set; }
    }
}

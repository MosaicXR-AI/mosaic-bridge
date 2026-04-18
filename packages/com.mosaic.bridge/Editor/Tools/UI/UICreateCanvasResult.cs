namespace Mosaic.Bridge.Tools.UI
{
    public sealed class UICreateCanvasResult
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string HierarchyPath { get; set; }
        public string RenderMode { get; set; }
        public bool EventSystemCreated { get; set; }
    }
}

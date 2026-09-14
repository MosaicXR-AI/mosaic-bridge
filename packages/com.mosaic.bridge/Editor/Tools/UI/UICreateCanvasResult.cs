namespace Mosaic.Bridge.Tools.UI
{
    public sealed class UICreateCanvasResult
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string HierarchyPath { get; set; }
        public string RenderMode { get; set; }
        public bool EventSystemCreated { get; set; }

        /// <summary>The actual input module type added — only meaningful when EventSystemCreated
        /// is true. Confirms what InputModule="auto" actually picked.</summary>
        public string InputModuleType { get; set; }
    }
}

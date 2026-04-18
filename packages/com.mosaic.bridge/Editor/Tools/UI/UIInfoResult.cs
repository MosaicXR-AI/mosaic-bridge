namespace Mosaic.Bridge.Tools.UI
{
    public sealed class UIInfoResult
    {
        public UICanvasInfo[] Canvases { get; set; }
    }

    public sealed class UICanvasInfo
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string RenderMode { get; set; }
        public UIElementInfo[] Children { get; set; }
    }

    public sealed class UIElementInfo
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string HierarchyPath { get; set; }
        public string[] Components { get; set; }
        public float[] AnchoredPosition { get; set; }
        public float[] SizeDelta { get; set; }
        public int ChildCount { get; set; }
    }
}

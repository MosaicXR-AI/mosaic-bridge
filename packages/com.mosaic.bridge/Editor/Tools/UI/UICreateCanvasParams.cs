namespace Mosaic.Bridge.Tools.UI
{
    public sealed class UICreateCanvasParams
    {
        /// <summary>Optional name for the Canvas GameObject. Defaults to "Canvas".</summary>
        public string Name { get; set; }

        /// <summary>Render mode: "Overlay", "Camera", or "WorldSpace". Defaults to "Overlay".</summary>
        public string RenderMode { get; set; }
    }
}

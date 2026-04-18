namespace Mosaic.Bridge.Tools.Settings
{
    public sealed class SettingsSetRenderResult
    {
        public string PreviousColorSpace { get; set; }
        public string NewColorSpace { get; set; }
        public bool RenderPipelineChanged { get; set; }
    }
}

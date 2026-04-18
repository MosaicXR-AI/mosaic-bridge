namespace Mosaic.Bridge.Tools.Settings
{
    public sealed class SettingsGetRenderResult
    {
        public string RenderPipelineAsset { get; set; }
        public string ColorSpace { get; set; }
        public bool HdrEnabled { get; set; }
        public string ActiveBuildTarget { get; set; }
        public string GraphicsApi { get; set; }
    }
}

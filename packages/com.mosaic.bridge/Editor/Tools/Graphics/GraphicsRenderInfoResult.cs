namespace Mosaic.Bridge.Tools.Graphics
{
    public sealed class GraphicsRenderInfoResult
    {
        public string RenderPipeline { get; set; }
        public string ColorSpace { get; set; }
        public string GraphicsApi { get; set; }
        public string QualityLevel { get; set; }
        public int QualityLevelIndex { get; set; }
        public string CurrentResolution { get; set; }
        public string GraphicsDeviceName { get; set; }
        public string GraphicsDeviceType { get; set; }
        public int GraphicsMemorySize { get; set; }
    }
}

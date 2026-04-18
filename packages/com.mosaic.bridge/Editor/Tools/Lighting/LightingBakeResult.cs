namespace Mosaic.Bridge.Tools.Lighting
{
    public sealed class LightingBakeResult
    {
        public bool Started { get; set; }
        public bool IsAsync { get; set; }
        public bool IsRunning { get; set; }
        public string Message { get; set; }
    }
}

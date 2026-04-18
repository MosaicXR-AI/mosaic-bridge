namespace Mosaic.Bridge.Tools.Profiling
{
    public sealed class ProfilerFrameDataResult
    {
        public float DeltaTime { get; set; }
        public float Fps { get; set; }
        public float RealtimeSinceStartup { get; set; }
        public int FrameCount { get; set; }
        public bool IsPlaying { get; set; }
    }
}

namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioPreviewResult
    {
        public string Action          { get; set; }
        public bool   IsPlaying       { get; set; }
        public float  PositionSeconds { get; set; }
        public string Message         { get; set; }
    }
}

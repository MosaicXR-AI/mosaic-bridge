#if MOSAIC_HAS_TIMELINE
namespace Mosaic.Bridge.Tools.Timeline
{
    public sealed class TimelineAddClipResult
    {
        public int TrackIndex { get; set; }
        public string ClipName { get; set; }
        public double Start { get; set; }
        public double Duration { get; set; }
    }
}
#endif

#if MOSAIC_HAS_TIMELINE
namespace Mosaic.Bridge.Tools.Timeline
{
    public sealed class TimelineAddClipResult
    {
        public int TrackIndex { get; set; }
        public string ClipName { get; set; }
        public double Start { get; set; }
        public double Duration { get; set; }
        public double ClipIn { get; set; }
        public double TimeScale { get; set; }
        public double EaseInDuration { get; set; }
        public double EaseOutDuration { get; set; }
        public double BlendInDuration { get; set; }
        public double BlendOutDuration { get; set; }
        public string BlendInCurveMode { get; set; }
        public string BlendOutCurveMode { get; set; }
    }
}
#endif

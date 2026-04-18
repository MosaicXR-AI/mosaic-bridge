#if MOSAIC_HAS_TIMELINE
namespace Mosaic.Bridge.Tools.Timeline
{
    public sealed class TimelineAddTrackResult
    {
        public int TrackIndex { get; set; }
        public string TrackType { get; set; }
        public string Name { get; set; }
    }
}
#endif

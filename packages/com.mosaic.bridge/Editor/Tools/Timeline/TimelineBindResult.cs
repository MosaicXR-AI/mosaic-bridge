#if MOSAIC_HAS_TIMELINE
namespace Mosaic.Bridge.Tools.Timeline
{
    public sealed class TimelineBindResult
    {
        public int DirectorInstanceId { get; set; }
        public int TrackIndex { get; set; }
        public string TrackName { get; set; }
        public int TargetInstanceId { get; set; }
        public string TargetName { get; set; }
    }
}
#endif

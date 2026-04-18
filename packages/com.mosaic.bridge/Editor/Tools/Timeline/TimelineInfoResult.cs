#if MOSAIC_HAS_TIMELINE
using System.Collections.Generic;

namespace Mosaic.Bridge.Tools.Timeline
{
    public sealed class TimelineInfoResult
    {
        public string AssetPath { get; set; }
        public string Name { get; set; }
        public double Duration { get; set; }
        public double FrameRate { get; set; }
        public int TrackCount { get; set; }
        public List<TimelineTrackInfo> Tracks { get; set; }
    }

    public sealed class TimelineTrackInfo
    {
        public int Index { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public bool Muted { get; set; }
        public int ClipCount { get; set; }
        public List<TimelineClipInfo> Clips { get; set; }
        public string BoundObjectName { get; set; }
    }

    public sealed class TimelineClipInfo
    {
        public string DisplayName { get; set; }
        public double Start { get; set; }
        public double Duration { get; set; }
        public double End { get; set; }
    }
}
#endif

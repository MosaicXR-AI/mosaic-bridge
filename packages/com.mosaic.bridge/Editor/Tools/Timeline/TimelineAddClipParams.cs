#if MOSAIC_HAS_TIMELINE
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Timeline
{
    public sealed class TimelineAddClipParams
    {
        [Required] public string AssetPath { get; set; }
        [Required] public int TrackIndex { get; set; }
        public string ClipAssetPath { get; set; }
        public double Start { get; set; }
        public double Duration { get; set; } = 1.0;
    }
}
#endif

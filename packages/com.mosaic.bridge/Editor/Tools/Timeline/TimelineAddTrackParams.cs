#if MOSAIC_HAS_TIMELINE
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Timeline
{
    public sealed class TimelineAddTrackParams
    {
        [Required] public string AssetPath { get; set; }
        [Required] public string TrackType { get; set; } // Animation, Audio, Activation, Signal, Control
        public string Name { get; set; }
    }
}
#endif

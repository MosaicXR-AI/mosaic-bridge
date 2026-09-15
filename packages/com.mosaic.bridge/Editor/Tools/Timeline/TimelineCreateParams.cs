#if MOSAIC_HAS_TIMELINE
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Timeline
{
    public sealed class TimelineCreateParams
    {
        [Required] public string Name { get; set; }
        [Required] public string Path { get; set; }

        /// <summary>Frames per second for framelocked preview, frame snapping and the time ruler. Null for Unity's default.</summary>
        public double? FrameRate { get; set; }

        /// <summary>"BasedOnClips" (default) or "FixedLength". Null to leave Unity's default.</summary>
        public string DurationMode { get; set; }

        /// <summary>Length in seconds when DurationMode is "FixedLength".</summary>
        public double? FixedDuration { get; set; }
    }
}
#endif

#if MOSAIC_HAS_TIMELINE
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Timeline
{
    public sealed class TimelineSetDirectorParams
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        [Required] public string TimelineAssetPath { get; set; }

        public bool? PlayOnAwake { get; set; }
        /// <summary>What happens when playback reaches the end: "Hold", "Loop", or "None". Null to leave unchanged.</summary>
        public string WrapMode { get; set; }
        /// <summary>Playback clock: "GameTime", "DSPClock", "UnscaledGameTime", or "Manual". Null to leave unchanged.</summary>
        public string UpdateMode { get; set; }
        public double? InitialTime { get; set; }
    }
}
#endif

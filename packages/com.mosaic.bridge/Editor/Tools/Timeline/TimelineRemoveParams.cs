#if MOSAIC_HAS_TIMELINE
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Timeline
{
    public sealed class TimelineRemoveParams
    {
        [Required] public string AssetPath { get; set; }

        /// <summary>What to remove: "track", "clip", or "marker".</summary>
        [Required] public string Target { get; set; }

        /// <summary>track: which track to delete (index into GetOutputTracks()). clip/marker: which
        /// track hosts the clip/marker. For marker, omit to use the timeline's own global marker track.</summary>
        public int? TrackIndex { get; set; }

        public int? ClipIndex { get; set; }
        public int? MarkerIndex { get; set; }
    }
}
#endif

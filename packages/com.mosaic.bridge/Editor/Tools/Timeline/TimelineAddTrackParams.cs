#if MOSAIC_HAS_TIMELINE
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Timeline
{
    public sealed class TimelineAddTrackParams
    {
        [Required] public string AssetPath { get; set; }

        /// <summary>Animation, Audio, Activation, Signal, Control, Group, Marker (the timeline's
        /// own global marker track — idempotent, ignores Name/ParentGroupName), Playable,
        /// Cinemachine (resolved via Type.GetType — no compile-time Cinemachine dependency; fails
        /// with NOT_FOUND if the package isn't installed), or Custom (with CustomTypeName).</summary>
        [Required] public string TrackType { get; set; }

        public string Name { get; set; }

        /// <summary>TrackType=Custom: assembly-qualified or bare name of a TrackAsset-derived type.</summary>
        public string CustomTypeName { get; set; }

        /// <summary>Nest the new track under an existing GroupTrack, found by name anywhere in the
        /// timeline (including inside other groups). Null/empty creates a root track.</summary>
        public string ParentGroupName { get; set; }

        public bool? Mute { get; set; }
    }
}
#endif

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

        // -- TimelineClip (any track) --

        /// <summary>Name shown on the clip in the Timeline window. Null to leave Unity's default.</summary>
        public string DisplayName { get; set; }
        /// <summary>Local offset into the source clip/asset where playback starts.</summary>
        public double? ClipIn { get; set; }
        /// <summary>Speed multiplier for the clip.</summary>
        public double? TimeScale { get; set; }
        public double? EaseInDuration { get; set; }
        public double? EaseOutDuration { get; set; }
        /// <summary>Overlap in seconds with the previous clip on the same track.</summary>
        public double? BlendInDuration { get; set; }
        /// <summary>Overlap in seconds with the next clip on the same track.</summary>
        public double? BlendOutDuration { get; set; }
        /// <summary>"Auto" (normalized against the opposing clip) or "Manual" (fixed curve). Null to leave unchanged.</summary>
        public string BlendInCurveMode { get; set; }
        public string BlendOutCurveMode { get; set; }

        // -- AnimationTrack clip (AnimationPlayableAsset) --

        /// <summary>Translational offset of the clip, as [x, y, z].</summary>
        public float[] AnimPosition { get; set; }
        /// <summary>Rotational offset of the clip, as [x, y, z] Euler angles.</summary>
        public float[] AnimEulerAngles { get; set; }
        /// <summary>"Off", "On", or "UseSourceAsset" (defer to the clip's own loop setting). Null to leave unchanged.</summary>
        public string AnimLoop { get; set; }
        public bool? AnimRemoveStartOffset { get; set; }
        /// <summary>Apply foot IK when the target Animator is humanoid.</summary>
        public bool? AnimApplyFootIK { get; set; }

        // -- AudioTrack clip (AudioPlayableAsset) --

        public bool? AudioLoop { get; set; }

        // -- ControlTrack clip (ControlPlayableAsset) --

        /// <summary>Whether Particle Systems under the controlled hierarchy are controlled by this clip.</summary>
        public bool? ControlUpdateParticle { get; set; }
        /// <summary>GameObject active state when Timeline stops: "Active", "Inactive", or "Revert". Null to leave unchanged.</summary>
        public string ControlPostPlayback { get; set; }
    }
}
#endif

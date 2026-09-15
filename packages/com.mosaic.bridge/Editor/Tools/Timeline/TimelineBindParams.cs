#if MOSAIC_HAS_TIMELINE
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Timeline
{
    public sealed class TimelineBindParams
    {
        /// <summary>"bind" (default): SetGenericBinding on a track. "set-reference": set an
        /// ExposedReference field on a clip's PlayableAsset (currently ControlPlayableAsset.sourceGameObject).</summary>
        public string Action { get; set; } = "bind";

        [Required] public int DirectorInstanceId { get; set; }

        /// <summary>Alternative to DirectorInstanceId: a bare name (searched across every loaded
        /// scene) or a "/"-separated hierarchy path. Used only when DirectorInstanceId is 0/omitted
        /// or does not resolve.</summary>
        public string DirectorPath { get; set; }

        [Required] public int TrackIndex { get; set; }

        // -- bind --

        /// <summary>GameObject or Component to bind. When the track's own TrackBindingTypeAttribute
        /// names a Component type and this resolves to a GameObject, the matching component is
        /// looked up automatically (e.g. binding an AnimationTrack to a GameObject finds its Animator).</summary>
        public int TargetInstanceId { get; set; }

        /// <summary>Alternative to TargetInstanceId: a bare name or "/"-separated hierarchy path.
        /// Used only when TargetInstanceId is 0/omitted or does not resolve. O4 §3.3 — instance-ID-only
        /// binding is why the agent used to fall back to a run-block for anything named in a build plan.</summary>
        public string TargetPath { get; set; }

        // -- set-reference --

        /// <summary>set-reference: index of the clip within TrackIndex's track whose PlayableAsset
        /// carries the ExposedReference field.</summary>
        public int ClipIndex { get; set; }

        /// <summary>set-reference: GameObject to assign to the ExposedReference. Same InstanceId/Path
        /// resolution as TargetInstanceId/TargetPath above.</summary>
        public int ReferenceInstanceId { get; set; }
        public string ReferencePath { get; set; }
    }
}
#endif

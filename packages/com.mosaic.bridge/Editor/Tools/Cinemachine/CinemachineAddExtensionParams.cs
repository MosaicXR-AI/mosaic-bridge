#if MOSAIC_HAS_CINEMACHINE
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Cinemachine
{
    public sealed class CinemachineAddExtensionParams
    {
        [Required] public string VCamName { get; set; }

        /// <summary>Confiner2D, Confiner3D, Deoccluder, Decollider, ImpulseListener,
        /// FreeLookModifier, Storyboard, FollowZoom, CameraOffset, PixelPerfect, Recomposer.</summary>
        [Required] public string ExtensionType { get; set; }

        // -- Confiner2D --
        /// <summary>Name of a scene GameObject carrying the Collider2D to confine within.</summary>
        public string ConfinerBoundingShapeName { get; set; }
        public float? ConfinerDamping { get; set; }

        // -- Confiner3D --
        /// <summary>Name of a scene GameObject carrying the Collider volume to confine within.</summary>
        public string ConfinerBoundingVolumeName { get; set; }

        // -- Deoccluder --
        /// <summary>Layer names (comma-separated) obstacles are detected on.</summary>
        public string DeoccluderCollideAgainst { get; set; }

        // -- Decollider --
        public float? DecolliderCameraRadius { get; set; }

        // -- ImpulseListener --
        public int? ImpulseChannelMask { get; set; }
        public float? ImpulseGain { get; set; }

        // -- FreeLookModifier -- (requires the vcam to already have an OrbitalFollow body)
        public float? FreeLookEasing { get; set; }

        // -- Storyboard --
        /// <summary>Asset path of a Texture to overlay.</summary>
        public string StoryboardImagePath { get; set; }
        public float? StoryboardAlpha { get; set; }
        public bool? StoryboardMuteCamera { get; set; }

        // -- FollowZoom --
        public float? FollowZoomWidth { get; set; }
        public float? FollowZoomFovMin { get; set; }
        public float? FollowZoomFovMax { get; set; }
        public float? FollowZoomDamping { get; set; }

        // -- CameraOffset --
        public float[] CameraOffsetValue { get; set; }

        // -- Recomposer --
        public float? RecomposerZoomScale { get; set; }
        public float? RecomposerTilt { get; set; }
        public float? RecomposerPan { get; set; }
        public float? RecomposerDutch { get; set; }
    }
}
#endif

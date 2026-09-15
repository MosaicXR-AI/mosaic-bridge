using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Animations
{
    public sealed class AnimationAvatarMaskParams
    {
        /// <summary>Action to perform: create, set-body-part, add-transform-path,
        /// remove-transform-path, info</summary>
        [Required] public string Action { get; set; }

        /// <summary>Asset path for the AvatarMask (e.g. "Assets/Animations/UpperBody.mask")</summary>
        [Required] public string MaskPath { get; set; }

        /// <summary>set-body-part: one of AvatarMaskBodyPart's names — Root, Body, Head, LeftLeg,
        /// RightLeg, LeftArm, RightArm, LeftFingers, RightFingers, LeftFootIK, RightFootIK,
        /// LeftHandIK, RightHandIK.</summary>
        public string BodyPart { get; set; }

        /// <summary>set-body-part: whether BodyPart is included in the mask.</summary>
        public bool? Active { get; set; }

        /// <summary>add-transform-path / remove-transform-path: name of the scene GameObject whose
        /// hierarchy TransformPath is resolved against (a rig's root, typically).</summary>
        public string RootGameObjectName { get; set; }

        /// <summary>Instance ID of the root GameObject. Takes priority over RootGameObjectName.</summary>
        public int? RootInstanceId { get; set; }

        /// <summary>add-transform-path / remove-transform-path: "/"-separated child path under the
        /// root (Transform.Find syntax, e.g. "Hips/Spine/Spine1") — empty string means the root
        /// transform itself.</summary>
        public string TransformPath { get; set; }

        /// <summary>add-transform-path: also include every descendant of the resolved transform
        /// (default true, matching AvatarMask.AddTransformPath's own default).</summary>
        public bool Recursive { get; set; } = true;
    }
}

using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Animations
{
    public sealed class AnimationAssignParams
    {
        /// <summary>Name of the target GameObject. Used if InstanceId is not set.</summary>
        public string GameObjectName { get; set; }
        /// <summary>Instance ID of the target GameObject. Takes priority over GameObjectName.</summary>
        public int? InstanceId { get; set; }

        /// <summary>Asset path of the AnimatorController to assign (e.g. "Assets/Anim/Player.controller").
        /// Ignored when Legacy is true.</summary>
        public string ControllerPath { get; set; }

        /// <summary>Asset path of the Avatar to assign — an FBX sub-asset for a Humanoid/Generic rig
        /// (e.g. "Assets/Models/Hero.fbx"). Ignored when Legacy is true.</summary>
        public string AvatarPath { get; set; }

        /// <summary>Animator.applyRootMotion. Ignored when Legacy is true.</summary>
        public bool? ApplyRootMotion { get; set; }

        /// <summary>Animator.updateMode: Normal, AnimatePhysics, or UnscaledTime. Ignored when Legacy is true.</summary>
        public string UpdateMode { get; set; }

        /// <summary>Animator.cullingMode: AlwaysAnimate, CullUpdateTransforms, or CullCompletely.
        /// Ignored when Legacy is true.</summary>
        public string CullingMode { get; set; }

        /// <summary>When true, targets the deprecated UnityEngine.Animation component instead of
        /// Animator — use ClipPath/ClipName instead of ControllerPath/AvatarPath in that case.</summary>
        public bool Legacy { get; set; }

        /// <summary>Legacy only: asset path of the AnimationClip to assign as Animation.clip.</summary>
        public string ClipPath { get; set; }

        /// <summary>Legacy only: for a multi-clip container, the specific clip name to use (see L19).</summary>
        public string ClipName { get; set; }
    }
}

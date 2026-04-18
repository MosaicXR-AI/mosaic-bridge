using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Animations
{
    public sealed class AnimationStateParams
    {
        /// <summary>Action to perform: add, remove, set-motion, info</summary>
        [Required] public string Action { get; set; }

        /// <summary>Asset path for the AnimatorController</summary>
        [Required] public string ControllerPath { get; set; }

        /// <summary>Layer index (default 0)</summary>
        public int LayerIndex { get; set; } = 0;

        /// <summary>State name (for add, remove, set-motion, info)</summary>
        public string StateName { get; set; }

        /// <summary>Asset path of the AnimationClip to assign as motion (for set-motion)</summary>
        public string ClipPath { get; set; }
    }
}

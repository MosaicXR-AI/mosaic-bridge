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

        /// <summary>Asset path of the AnimationClip to assign as motion (for set-motion). For a
        /// multi-clip FBX, this alone always resolves to the FIRST embedded clip — pass ClipName
        /// to pick a specific take.</summary>
        public string ClipPath { get; set; }

        /// <summary>set-motion only: name of the specific clip to use when ClipPath is a
        /// multi-clip container (e.g. an imported FBX with several takes/animations embedded).
        /// Omit when ClipPath already names or resolves to a single clip.</summary>
        public string ClipName { get; set; }
    }
}

using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Animations
{
    public sealed class AnimationStateParams
    {
        /// <summary>Action to perform: add, remove, set-motion, info, add-sub-machine, set-default,
        /// set-settings</summary>
        [Required] public string Action { get; set; }

        /// <summary>Asset path for the AnimatorController</summary>
        [Required] public string ControllerPath { get; set; }

        /// <summary>Layer index (default 0)</summary>
        public int LayerIndex { get; set; } = 0;

        /// <summary>State name (for add, remove, set-motion, info, set-default, set-settings)</summary>
        public string StateName { get; set; }

        /// <summary>"/"-separated path of sub-state-machine names (each level's own name, e.g.
        /// "Combat/Melee") locating the state machine to operate in — the layer's own root state
        /// machine when omitted. Every generated state used to land at the layer root with no way
        /// to place it inside a sub-machine, making Animator captures of anything but a flat
        /// state machine unreadable. Used by add (where the new state/sub-machine is created),
        /// add-sub-machine (ditto), and as a scope hint for remove/set-motion/info/set-default/
        /// set-settings (search starts at this machine instead of the whole layer).</summary>
        public string ParentStateMachinePath { get; set; }

        /// <summary>add / add-sub-machine: [x, y] position in the Animator graph. Omit for (0,0) —
        /// the pre-existing default every generated state used to be stuck at.</summary>
        public float[] Position { get; set; }

        /// <summary>add-sub-machine: name of the new sub-state-machine.</summary>
        public string SubMachineName { get; set; }

        /// <summary>Asset path of the AnimationClip to assign as motion (for set-motion). For a
        /// multi-clip FBX, this alone always resolves to the FIRST embedded clip — pass ClipName
        /// to pick a specific take.</summary>
        public string ClipPath { get; set; }

        /// <summary>set-motion only: name of the specific clip to use when ClipPath is a
        /// multi-clip container (e.g. an imported FBX with several takes/animations embedded).
        /// Omit when ClipPath already names or resolves to a single clip.</summary>
        public string ClipName { get; set; }

        // -- set-settings (AnimatorState) --
        public float? Speed { get; set; }
        public string SpeedParameter { get; set; }
        public bool? SpeedParameterActive { get; set; }
        public float? CycleOffset { get; set; }
        public string CycleOffsetParameter { get; set; }
        public bool? CycleOffsetParameterActive { get; set; }
        public bool? Mirror { get; set; }
        public string MirrorParameter { get; set; }
        public bool? MirrorParameterActive { get; set; }
        public bool? IKOnFeet { get; set; }
        public bool? WriteDefaultValues { get; set; }
        public string Tag { get; set; }
    }
}

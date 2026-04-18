using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Animations
{
    public sealed class AnimationTransitionParams
    {
        /// <summary>Action to perform: add, remove, set-conditions</summary>
        [Required] public string Action { get; set; }

        /// <summary>Asset path for the AnimatorController</summary>
        [Required] public string ControllerPath { get; set; }

        /// <summary>Layer index (default 0)</summary>
        public int LayerIndex { get; set; } = 0;

        /// <summary>Source state name</summary>
        public string SourceStateName { get; set; }

        /// <summary>Destination state name</summary>
        public string DestinationStateName { get; set; }

        /// <summary>Transition index within the source state (for remove)</summary>
        public int? TransitionIndex { get; set; }

        /// <summary>Has exit time (default true)</summary>
        public bool HasExitTime { get; set; } = true;

        /// <summary>Transition duration in seconds (default 0.25)</summary>
        public float TransitionDuration { get; set; } = 0.25f;

        /// <summary>Conditions to set on the transition (for set-conditions)</summary>
        public TransitionConditionInput[] Conditions { get; set; }
    }

    public sealed class TransitionConditionInput
    {
        /// <summary>Parameter name</summary>
        public string ParameterName { get; set; }

        /// <summary>Condition mode: If, IfNot, Greater, Less, Equals, NotEqual</summary>
        public string Mode { get; set; }

        /// <summary>Threshold value (for Greater/Less/Equals/NotEqual)</summary>
        public float Threshold { get; set; }
    }
}

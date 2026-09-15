using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Animations
{
    public sealed class AnimationTransitionParams
    {
        /// <summary>Action to perform: add, remove, set-conditions, set-settings, info</summary>
        [Required] public string Action { get; set; }

        /// <summary>Asset path for the AnimatorController</summary>
        [Required] public string ControllerPath { get; set; }

        /// <summary>Layer index (default 0)</summary>
        public int LayerIndex { get; set; } = 0;

        /// <summary>Source state name. Ignored when SourceKind is "anyState" or "entry" (those
        /// transitions live on the state machine itself, not on a named state).</summary>
        public string SourceStateName { get; set; }

        /// <summary>add: "state" (default), "anyState", or "entry".
        /// remove / set-conditions / set-settings / info: which array TransitionIndex addresses
        /// — "state" (SourceStateName.transitions, default), "anyState"
        /// (stateMachine.anyStateTransitions), or "entry" (stateMachine.entryTransitions).
        /// "entry" transitions are Unity's AnimatorTransition, not AnimatorStateTransition — they
        /// have no ExitTime/Duration/interruption settings, only Conditions.</summary>
        public string SourceKind { get; set; }

        /// <summary>Destination state name. Ignored when DestinationKind is "exit".</summary>
        public string DestinationStateName { get; set; }

        /// <summary>add only: "state" (default) or "exit" (AnimatorState.AddExitTransition — a
        /// transition out of the state machine itself). Only valid when SourceKind is "state".</summary>
        public string DestinationKind { get; set; }

        /// <summary>Transition index within the array SourceKind selects (for remove, set-conditions,
        /// set-settings).</summary>
        public int? TransitionIndex { get; set; }

        /// <summary>Has exit time (default true). Ignored for SourceKind "entry".</summary>
        public bool HasExitTime { get; set; } = true;

        /// <summary>add only: transition duration in seconds (default 0.25). Ignored for
        /// SourceKind "entry". Use Duration (nullable) for set-settings instead — TransitionDuration's
        /// own non-nullable default would be indistinguishable from "explicitly set to 0.25".</summary>
        public float TransitionDuration { get; set; } = 0.25f;

        /// <summary>set-settings only: transition duration in seconds. Omit to leave unchanged.</summary>
        public float? Duration { get; set; }

        /// <summary>Conditions to set on the transition (for add / set-conditions)</summary>
        public TransitionConditionInput[] Conditions { get; set; }

        // -- set-settings (and applied on add too, for SourceKind "state"/"anyState") --
        // AnimatorStateTransition-only fields: not available on an "entry" AnimatorTransition.

        /// <summary>Normalized exit time (e.g. 0.9 = 90% through the clip). Meaningful only when
        /// HasExitTime is true.</summary>
        public float? ExitTime { get; set; }

        /// <summary>true = TransitionDuration is in seconds; false = normalized (fraction of the
        /// source state's length).</summary>
        public bool? HasFixedDuration { get; set; }

        /// <summary>Normalized time offset into the destination state's clip to start playback at.</summary>
        public float? Offset { get; set; }

        /// <summary>"None" (default), "Source", "Destination", "SourceThenDestination", or
        /// "DestinationThenSource".</summary>
        public string InterruptionSource { get; set; }

        /// <summary>When multiple transitions can interrupt, whether they're evaluated in order.</summary>
        public bool? OrderedInterruption { get; set; }

        /// <summary>Whether a state can transition to itself (only meaningful for AnyState transitions).</summary>
        public bool? CanTransitionToSelf { get; set; }

        public bool? Mute { get; set; }
        public bool? Solo { get; set; }
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

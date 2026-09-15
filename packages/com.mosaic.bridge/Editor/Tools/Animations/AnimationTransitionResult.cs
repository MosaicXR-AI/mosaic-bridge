namespace Mosaic.Bridge.Tools.Animations
{
    public sealed class AnimationTransitionResult
    {
        public string Action { get; set; }
        public string ControllerPath { get; set; }
        public string SourceStateName { get; set; }
        public string DestinationStateName { get; set; }
        public int LayerIndex { get; set; }
        public string SourceKind { get; set; }
        public bool IsExit { get; set; }
        public int? TransitionIndex { get; set; }
        public bool HasExitTime { get; set; }
        public float TransitionDuration { get; set; }
        public int ConditionCount { get; set; }

        // -- settings (AnimatorStateTransition only; null/default for an "entry" transition) --
        public float? ExitTime { get; set; }
        public bool? HasFixedDuration { get; set; }
        public float? Offset { get; set; }
        public string InterruptionSource { get; set; }
        public bool? OrderedInterruption { get; set; }
        public bool? CanTransitionToSelf { get; set; }
        public bool? Mute { get; set; }
        public bool? Solo { get; set; }

        /// <summary>Set only for the 'info' action.</summary>
        public AnimationTransitionInfo[] Transitions { get; set; }
    }

    public sealed class AnimationTransitionInfo
    {
        public string SourceKind { get; set; }
        public int TransitionIndex { get; set; }
        public string DestinationStateName { get; set; }
        public bool IsExit { get; set; }
        public bool HasExitTime { get; set; }
        public float? ExitTime { get; set; }
        public float TransitionDuration { get; set; }
        public bool? HasFixedDuration { get; set; }
        public float? Offset { get; set; }
        public string InterruptionSource { get; set; }
        public bool? Mute { get; set; }
        public bool? Solo { get; set; }
        public int ConditionCount { get; set; }
    }
}

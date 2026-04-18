namespace Mosaic.Bridge.Tools.Animations
{
    public sealed class AnimationTransitionResult
    {
        public string Action { get; set; }
        public string ControllerPath { get; set; }
        public string SourceStateName { get; set; }
        public string DestinationStateName { get; set; }
        public int LayerIndex { get; set; }
        public bool HasExitTime { get; set; }
        public float TransitionDuration { get; set; }
        public int ConditionCount { get; set; }
    }
}

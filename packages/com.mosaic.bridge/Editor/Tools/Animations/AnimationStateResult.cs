namespace Mosaic.Bridge.Tools.Animations
{
    public sealed class AnimationStateResult
    {
        public string Action { get; set; }
        public string ControllerPath { get; set; }
        public string StateName { get; set; }
        public int LayerIndex { get; set; }

        // -- info --
        public string MotionName { get; set; }
        public string MotionPath { get; set; }
        public float Speed { get; set; }
        public string Tag { get; set; }
        public int TransitionCount { get; set; }
        public bool IsDefault { get; set; }

        // -- add --
        public float PositionX { get; set; }
        public float PositionY { get; set; }
    }
}

namespace Mosaic.Bridge.Tools.Animations
{
    public sealed class AnimationBlendTreeResult
    {
        public string Action { get; set; }
        public string ControllerPath { get; set; }
        public string StateName { get; set; }
        public int LayerIndex { get; set; }
        public string BlendType { get; set; }
        public string BlendParameter { get; set; }
        public string BlendParameterY { get; set; }
        public int ChildCount { get; set; }
        public BlendTreeChildInfo[] Children { get; set; }

        // -- set-thresholds --
        public bool UseAutomaticThresholds { get; set; }
        public float MinThreshold { get; set; }
        public float MaxThreshold { get; set; }

        // -- add-child-tree --
        public string ChildTreeName { get; set; }
    }

    public sealed class BlendTreeChildInfo
    {
        public string ClipName { get; set; }
        public string ClipPath { get; set; }
        public float Threshold { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
        public float TimeScale { get; set; }
        public string DirectBlendParameter { get; set; }
        public bool Mirror { get; set; }
        public bool IsNestedBlendTree { get; set; }
    }
}

using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Animations
{
    public sealed class AnimationBlendTreeParams
    {
        /// <summary>Action to perform: create, info, set-children, set-thresholds, add-child-tree</summary>
        [Required] public string Action { get; set; }

        /// <summary>Asset path for the AnimatorController containing the blend tree</summary>
        [Required] public string ControllerPath { get; set; }

        /// <summary>Layer index (default 0)</summary>
        public int LayerIndex { get; set; } = 0;

        /// <summary>State name that hosts (or will host) the blend tree</summary>
        public string StateName { get; set; }

        /// <summary>Blend type: Simple1D, SimpleDirectional2D, FreeformDirectional2D, FreeformCartesian2D, Direct</summary>
        public string BlendType { get; set; } = "Simple1D";

        /// <summary>Blend parameter name (X axis for 1D and 2D)</summary>
        public string BlendParameter { get; set; }

        /// <summary>Second blend parameter name (Y axis for 2D types)</summary>
        public string BlendParameterY { get; set; }

        /// <summary>Children to set (for set-children)</summary>
        public BlendTreeChildInput[] Children { get; set; }

        // -- set-thresholds --
        /// <summary>When true, children's 1D thresholds are spread automatically between
        /// MinThreshold and MaxThreshold instead of using each child's own explicit Threshold.</summary>
        public bool? UseAutomaticThresholds { get; set; }
        public float? MinThreshold { get; set; }
        public float? MaxThreshold { get; set; }

        // -- add-child-tree --
        /// <summary>Blend type of the new nested BlendTree (same valid values as the top-level
        /// BlendType).</summary>
        public string ChildBlendType { get; set; } = "Simple1D";
        public string ChildBlendParameter { get; set; }
        public string ChildBlendParameterY { get; set; }
        /// <summary>Where the nested tree sits in ITS PARENT: a 1D threshold, or (with PositionX/
        /// PositionY) a 2D position — same fields BlendTreeChildInput uses for a normal clip child.</summary>
        public float Threshold { get; set; }
        public float PositionX { get; set; }
        public float PositionY { get; set; }
    }

    public sealed class BlendTreeChildInput
    {
        /// <summary>Asset path to the AnimationClip</summary>
        public string ClipPath { get; set; }

        /// <summary>Threshold for 1D blend trees</summary>
        public float Threshold { get; set; }

        /// <summary>2D position X</summary>
        public float PositionX { get; set; }

        /// <summary>2D position Y</summary>
        public float PositionY { get; set; }

        /// <summary>Time scale (default 1)</summary>
        public float TimeScale { get; set; } = 1f;

        /// <summary>Parameter used by this child when the tree's own BlendType is Direct — each
        /// child blends independently by its own parameter instead of the tree's shared one.</summary>
        public string DirectBlendParameter { get; set; }

        /// <summary>Mirror this child's motion (Humanoid rigs only).</summary>
        public bool Mirror { get; set; }
    }
}

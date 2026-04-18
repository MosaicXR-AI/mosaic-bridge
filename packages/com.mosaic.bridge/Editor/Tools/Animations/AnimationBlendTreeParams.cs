using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Animations
{
    public sealed class AnimationBlendTreeParams
    {
        /// <summary>Action to perform: create, info, set-children</summary>
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
    }
}

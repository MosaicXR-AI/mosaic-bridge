using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Animations
{
    public sealed class AnimationIkSetupParams
    {
        [Required] public string GameObjectName { get; set; }

        /// <summary>IK solver: "fabrik", "ccd", or "limb".</summary>
        [Required] public string Solver { get; set; }

        /// <summary>Transform paths from root to end-effector (at least 2 bones).</summary>
        [Required] public string[] ChainBones { get; set; }

        /// <summary>Target GameObject name.</summary>
        [Required] public string Target { get; set; }

        /// <summary>Optional pole target GameObject name (used by limb solver).</summary>
        public string Pole { get; set; }

        public int Iterations { get; set; } = 10;
        public float Tolerance { get; set; } = 0.001f;

        /// <summary>Optional JSON describing per-bone constraints.</summary>
        public string ConstraintsJson { get; set; }

        /// <summary>Generated script name (without extension).</summary>
        public string Name { get; set; }

        public string SavePath { get; set; } = "Assets/Generated/Animation/";
    }
}

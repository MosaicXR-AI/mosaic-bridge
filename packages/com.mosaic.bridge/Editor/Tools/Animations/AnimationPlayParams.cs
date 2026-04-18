using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Animations
{
    public sealed class AnimationPlayParams
    {
        /// <summary>Action to perform: play, stop, sample</summary>
        [Required] public string Action { get; set; }

        /// <summary>Name of the target GameObject (must have an Animator component)</summary>
        public string GameObjectName { get; set; }

        /// <summary>Instance ID of the target GameObject (alternative to GameObjectName)</summary>
        public int? InstanceId { get; set; }

        /// <summary>State name to play (for play action)</summary>
        public string StateName { get; set; }

        /// <summary>Layer index (default 0, for play)</summary>
        public int LayerIndex { get; set; } = 0;

        /// <summary>Normalized time to sample at (0..1, for sample action)</summary>
        public float? NormalizedTime { get; set; }

        /// <summary>Asset path to an AnimationClip to preview (for sample, alternative to StateName)</summary>
        public string ClipPath { get; set; }
    }
}

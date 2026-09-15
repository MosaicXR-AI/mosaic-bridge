#if MOSAIC_HAS_CINEMACHINE
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Cinemachine
{
    public sealed class CinemachineStateDrivenInstructionInput
    {
        /// <summary>"Layer.State" path, hashed via Animator.StringToHash to match FullHash.</summary>
        [Required] public string StateName { get; set; }
        /// <summary>Index into ChildVCamNames — the camera this state activates.</summary>
        [Required] public int CameraIndex { get; set; }
        public float ActivateAfter { get; set; }
        public float MinDuration { get; set; }
    }

    public sealed class CinemachineSequencerInstructionInput
    {
        /// <summary>Index into ChildVCamNames — the camera this step activates.</summary>
        [Required] public int CameraIndex { get; set; }
        public float Hold { get; set; }
        /// <summary>Cut, EaseInOut, Linear. Default EaseInOut.</summary>
        public string BlendType { get; set; } = "EaseInOut";
        public float BlendTime { get; set; } = 2f;
    }

    public sealed class CinemachineCreateManagerParams
    {
        /// <summary>StateDriven, Mixing, Sequencer, ClearShot.</summary>
        [Required] public string ManagerType { get; set; }

        [Required] public string Name { get; set; }

        /// <summary>Names of existing vcam GameObjects to reparent under the new manager — a manager
        /// camera only sees its own hierarchy children.</summary>
        [Required] public string[] ChildVCamNames { get; set; }

        public int Priority { get; set; } = 10;

        // -- StateDriven --
        /// <summary>Name of the GameObject carrying the Animator whose state changes drive camera choice.</summary>
        public string AnimatedTargetName { get; set; }
        public int LayerIndex { get; set; }
        public CinemachineStateDrivenInstructionInput[] StateDrivenInstructions { get; set; }

        // -- Sequencer --
        public bool? Loop { get; set; }
        public CinemachineSequencerInstructionInput[] SequencerInstructions { get; set; }

        // -- ClearShot --
        public float? ActivateAfter { get; set; }
        public float? MinDuration { get; set; }
        public bool? RandomizeChoice { get; set; }
    }
}
#endif

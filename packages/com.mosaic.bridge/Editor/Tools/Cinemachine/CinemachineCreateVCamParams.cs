#if MOSAIC_HAS_CINEMACHINE
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Cinemachine
{
    public sealed class CinemachineCreateVCamParams
    {
        [Required] public string Name { get; set; }

        /// <summary>Name of the GameObject to follow. Null means no follow target.</summary>
        public string FollowTarget { get; set; }

        /// <summary>Name of the GameObject to look at. Null means no look-at target.</summary>
        public string LookAtTarget { get; set; }

        /// <summary>
        /// Body behavior type: ThirdPersonFollow, OrbitalFollow, PositionComposer, Follow (the most
        /// common body — a fixed FollowOffset from the target), HardLockToTarget.
        /// Null means no body component added (default Cinemachine behavior).
        /// </summary>
        public string BodyType { get; set; }

        /// <summary>BodyType=Follow only: the offset to maintain from the target, as [x, y, z].</summary>
        public float[] FollowOffset { get; set; }

        /// <summary>
        /// Aim behavior type: Composer, HardLookAt, GroupFraming, PanTilt, RotateWithFollowTarget.
        /// Null means no aim component added (default Cinemachine behavior).
        /// </summary>
        public string AimType { get; set; }

        /// <summary>AimType=PanTilt only: initial pan (Y-axis rotation) in degrees.</summary>
        public float? PanAngle { get; set; }
        /// <summary>AimType=PanTilt only: initial tilt (X-axis rotation) in degrees.</summary>
        public float? TiltAngle { get; set; }

        /// <summary>Noise behavior type: BasicMultiChannelPerlin. Null means no noise component added.</summary>
        public string NoiseType { get; set; }
        /// <summary>Asset path of an existing NoiseSettings profile.</summary>
        public string NoiseProfilePath { get; set; }
        /// <summary>Name of a NoiseSettings asset (project or package presets) to search for by
        /// AssetDatabase name when NoiseProfilePath is not given — e.g. "Handheld", "6D Shake Medium".</summary>
        public string NoiseProfilePresetName { get; set; }
        public float? NoiseAmplitudeGain { get; set; }
        public float? NoiseFrequencyGain { get; set; }

        // -- Lens block --
        public float? Dutch { get; set; }
        public float? OrthographicSize { get; set; }
        /// <summary>"None", "Orthographic", "Perspective", or "Physical". Null to leave unchanged.</summary>
        public string LensModeOverride { get; set; }

        /// <summary>Adds a CinemachineInputAxisController and auto-discovers the vcam's own input
        /// axes (e.g. PanTilt's Pan/Tilt) via SynchronizeControllers — without this, an orbit/look
        /// camera is inert in Play mode even with a body/aim that has input axes.</summary>
        public bool AddInputController { get; set; }

        /// <summary>Binds a discovered controller (by the name SynchronizeControllers gave it, e.g.
        /// "Look X (Pan)") to a specific InputActionReference asset, for custom input action maps
        /// instead of the default ones SynchronizeControllers wires up.</summary>
        public CinemachineInputControllerBindingInput[] InputControllerBindings { get; set; }

        /// <summary>Camera priority. Higher priority cameras take precedence. Default 10.</summary>
        public int Priority { get; set; } = 10;
    }

    public sealed class CinemachineInputControllerBindingInput
    {
        [Required] public string ControllerName { get; set; }
        [Required] public string InputActionReferencePath { get; set; }
        public float? Gain { get; set; }
    }
}
#endif

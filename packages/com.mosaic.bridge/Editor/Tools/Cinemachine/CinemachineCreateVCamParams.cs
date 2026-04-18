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
        /// Body behavior type: ThirdPersonFollow, OrbitalFollow, PositionComposer.
        /// Null means no body component added (default Cinemachine behavior).
        /// </summary>
        public string BodyType { get; set; }

        /// <summary>
        /// Aim behavior type: Composer, HardLookAt, GroupFraming.
        /// Null means no aim component added (default Cinemachine behavior).
        /// </summary>
        public string AimType { get; set; }

        /// <summary>Camera priority. Higher priority cameras take precedence. Default 10.</summary>
        public int Priority { get; set; } = 10;
    }
}
#endif

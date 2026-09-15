#if MOSAIC_HAS_CINEMACHINE
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Cinemachine
{
    public sealed class CinemachineTargetGroupParams
    {
        /// <summary>Action: create, add-member, remove-member, info.</summary>
        [Required] public string Action { get; set; }

        /// <summary>Name of the target group's GameObject. Required for add-member, remove-member,
        /// info; used as the new GameObject's name for create.</summary>
        [Required] public string Name { get; set; }

        // -- create --
        /// <summary>"GroupAverage" (default) or "GroupCenter". Null leaves Unity's default.</summary>
        public string PositionMode { get; set; }
        /// <summary>"GroupAverage" (default) or "Manual". Null leaves Unity's default.</summary>
        public string RotationMode { get; set; }

        // -- add-member / remove-member --
        public string MemberName { get; set; }
        public float Weight { get; set; } = 1f;
        public float Radius { get; set; } = 1f;
    }
}
#endif

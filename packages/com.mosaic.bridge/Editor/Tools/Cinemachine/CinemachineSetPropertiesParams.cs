#if MOSAIC_HAS_CINEMACHINE
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Cinemachine
{
    public sealed class CinemachineSetPropertiesParams
    {
        /// <summary>Name of the virtual camera GameObject.</summary>
        [Required] public string VCamName { get; set; }

        /// <summary>Camera priority. Null leaves unchanged.</summary>
        public int? Priority { get; set; }

        /// <summary>Field of view override. Null leaves unchanged.</summary>
        public float? FieldOfView { get; set; }

        /// <summary>Near clip plane. Null leaves unchanged.</summary>
        public float? NearClip { get; set; }

        /// <summary>Far clip plane. Null leaves unchanged.</summary>
        public float? FarClip { get; set; }

        /// <summary>Follow offset [x,y,z]. Null leaves unchanged.</summary>
        public float[] FollowOffset { get; set; }

        /// <summary>Damping [x,y,z]. Null leaves unchanged.</summary>
        public float[] Damping { get; set; }

        /// <summary>Follow target name to reassign. Null leaves unchanged.</summary>
        public string FollowTarget { get; set; }

        /// <summary>LookAt target name to reassign. Null leaves unchanged.</summary>
        public string LookAtTarget { get; set; }
    }
}
#endif

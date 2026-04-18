#if MOSAIC_HAS_CINEMACHINE
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Cinemachine
{
    public sealed class CinemachineCreateDollyParams
    {
        /// <summary>Name for the dolly track GameObject.</summary>
        [Required] public string Name { get; set; }

        /// <summary>
        /// Waypoints as a flat array of floats [x1,y1,z1, x2,y2,z2, ...].
        /// Must have at least 2 waypoints (6 floats).
        /// </summary>
        [Required] public float[] Waypoints { get; set; }

        /// <summary>Whether to enable auto-dolly (automatic position on spline). Default false.</summary>
        public bool AutoDolly { get; set; }

        /// <summary>Optional name of the virtual camera to attach the dolly to. Null creates standalone track.</summary>
        public string VCamName { get; set; }
    }
}
#endif

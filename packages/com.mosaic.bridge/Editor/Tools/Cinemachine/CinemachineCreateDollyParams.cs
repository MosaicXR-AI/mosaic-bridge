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

        // -- CinemachineSplineDolly fields (VCamName only) --

        /// <summary>Position along the spline, in PositionUnits. Null leaves Unity's default (0).</summary>
        public float? CameraPosition { get; set; }
        /// <summary>"Distance", "Normalized", or "Knot". Null leaves Unity's default (Normalized).</summary>
        public string PositionUnits { get; set; }
        /// <summary>[x, y, z] offset from the spline: x perpendicular, y up, z along-spline.</summary>
        public float[] SplineOffset { get; set; }
        /// <summary>"Default", "FollowTarget", "FollowTargetNoRoll", "Spline", "SplineNoRoll". Null leaves Unity's default.</summary>
        public string CameraRotation { get; set; }
        public bool? DampingEnabled { get; set; }
        public float[] DampingPosition { get; set; }
        public float? DampingAngular { get; set; }

        /// <summary>Auto-dolly implementation: "FixedSpeed" (AutoDollySpeed) or "NearestPointToTarget"
        /// (AutoDollyPositionOffset, needs a Follow target set on the vcam). Requires AutoDolly=true.
        /// Null uses Cinemachine's own default (no movement — just an on/off flag with no method).</summary>
        public string AutoDollyMethod { get; set; }
        public float? AutoDollySpeed { get; set; }
        public float? AutoDollyPositionOffset { get; set; }

        // -- add-cart --
        /// <summary>When given, also creates a CinemachineSplineCart GameObject riding the same
        /// spline — for a moving platform or non-camera rider, distinct from the camera dolly.</summary>
        public string CartName { get; set; }
        public float? CartSplinePosition { get; set; }
        /// <summary>"Distance", "Normalized", or "Knot". Null leaves Unity's default.</summary>
        public string CartPositionUnits { get; set; }
    }
}
#endif

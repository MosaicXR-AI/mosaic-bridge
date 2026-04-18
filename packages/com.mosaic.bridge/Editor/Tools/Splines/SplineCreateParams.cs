#if MOSAIC_HAS_SPLINES
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Splines
{
    public sealed class SplineCreateParams
    {
        /// <summary>Name of the SplineContainer GameObject.</summary>
        [Required] public string Name { get; set; }

        /// <summary>
        /// Array of knot definitions. Each knot has:
        /// Position (float[3]), Rotation (float[4] quaternion, optional),
        /// TangentIn (float[3], optional), TangentOut (float[3], optional).
        /// </summary>
        public SplineKnotData[] Knots { get; set; }

        /// <summary>Whether the spline forms a closed loop.</summary>
        public bool Closed { get; set; } = false;
    }

    public sealed class SplineKnotData
    {
        /// <summary>Position [x, y, z].</summary>
        [Required] public float[] Position { get; set; }

        /// <summary>Rotation as quaternion [x, y, z, w]. Default identity.</summary>
        public float[] Rotation { get; set; }

        /// <summary>Tangent in direction [x, y, z]. Default [0,0,0].</summary>
        public float[] TangentIn { get; set; }

        /// <summary>Tangent out direction [x, y, z]. Default [0,0,0].</summary>
        public float[] TangentOut { get; set; }
    }
}
#endif

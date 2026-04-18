using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsRaycastParams
    {
        /// <summary>Ray origin [x,y,z].</summary>
        [Required] public float[] Origin { get; set; }
        /// <summary>Ray direction [x,y,z].</summary>
        [Required] public float[] Direction { get; set; }
        /// <summary>Maximum raycast distance. Defaults to Mathf.Infinity.</summary>
        public float? MaxDistance { get; set; }
        /// <summary>Optional layer mask for filtering. Defaults to all layers (-1).</summary>
        public int? LayerMask { get; set; }
    }
}

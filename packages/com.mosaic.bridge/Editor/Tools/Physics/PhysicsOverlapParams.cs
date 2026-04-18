using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsOverlapParams
    {
        /// <summary>Overlap shape: sphere, box, or capsule.</summary>
        [Required] public string Action { get; set; }
        /// <summary>Center position [x,y,z].</summary>
        [Required] public float[] Position { get; set; }
        /// <summary>Radius for sphere overlap.</summary>
        public float? Radius { get; set; }
        /// <summary>Half-extents [x,y,z] for box overlap.</summary>
        public float[] Size { get; set; }
        /// <summary>Height for capsule overlap (total height including hemispheres).</summary>
        public float? Height { get; set; }
        /// <summary>Optional layer mask for filtering. Defaults to all layers.</summary>
        public int? LayerMask { get; set; }
    }
}

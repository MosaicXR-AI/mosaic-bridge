using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsAddColliderParams
    {
        /// <summary>Name of the target GameObject. Used if InstanceId is not set.</summary>
        public string Name { get; set; }
        /// <summary>Instance ID of the target GameObject. Takes priority over Name.</summary>
        public int? InstanceId { get; set; }
        /// <summary>Collider type: Box, Sphere, Capsule, or Mesh.</summary>
        [Required] public string Type { get; set; }
        /// <summary>Whether this collider is a trigger.</summary>
        public bool? IsTrigger { get; set; }
        /// <summary>Center offset [x,y,z]. Null leaves the default.</summary>
        public float[] Center { get; set; }
        /// <summary>Size for BoxCollider [x,y,z]. Ignored for other types.</summary>
        public float[] Size { get; set; }
    }
}

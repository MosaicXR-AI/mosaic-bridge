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
        /// <summary>When true, also adds a Rigidbody if one is not already present.</summary>
        public bool? AddRigidbody { get; set; }
        /// <summary>Mesh collider only. Unity REQUIRES this to be true when the GameObject has (or
        /// will have, via AddRigidbody) a non-kinematic Rigidbody — a concave MeshCollider on a
        /// dynamic Rigidbody is invalid and silently produces no collision. Default false, matching
        /// MeshCollider's own default.</summary>
        public bool Convex { get; set; }
    }
}

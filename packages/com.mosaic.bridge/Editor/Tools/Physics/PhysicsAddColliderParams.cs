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

        /// <summary>Sphere/Capsule only. Explicit override — null keeps the auto-fit size.</summary>
        public float? Radius { get; set; }
        /// <summary>Capsule only. Explicit override — null keeps the auto-fit size.</summary>
        public float? Height { get; set; }
        /// <summary>Capsule only. Axis: "X", "Y", or "Z". Null keeps CapsuleCollider's default (Y).</summary>
        public string Direction { get; set; }

        /// <summary>Asset path of a PhysicsMaterial to assign (sharedMaterial). Null leaves unset.</summary>
        public string MaterialPath { get; set; }

        /// <summary>Comma-separated layer names this collider additionally includes, overriding the
        /// Layer Collision Matrix (Collider.includeLayers). Null leaves the default (no override).</summary>
        public string IncludeLayers { get; set; }
        /// <summary>Comma-separated layer names this collider excludes (Collider.excludeLayers).</summary>
        public string ExcludeLayers { get; set; }

        /// <summary>"self" (default, fit to this GameObject's own mesh/renderer) or "children"
        /// (fit to the union of all child renderer bounds — a compound-collider approximation for
        /// a parent with no mesh of its own). Box colliders only.</summary>
        public string Fit { get; set; } = "self";
    }
}

using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Physics2D
{
    public sealed class Physics2DAddColliderParams
    {
        public string Name { get; set; }
        public int? InstanceId { get; set; }

        /// <summary>"Box", "Circle", "Capsule", "Polygon", or "Edge".</summary>
        [Required] public string ColliderType { get; set; }

        public bool? IsTrigger { get; set; }
        public float[] Offset { get; set; }

        /// <summary>Box/Capsule: [w, h]. Auto-fit to the GameObject's SpriteRenderer bounds when
        /// omitted and one is present.</summary>
        public float[] Size { get; set; }

        /// <summary>Box only: rounds the corners.</summary>
        public float? EdgeRadius { get; set; }

        /// <summary>Circle only. Auto-fit to the SpriteRenderer bounds when omitted.</summary>
        public float? Radius { get; set; }

        /// <summary>Edge (required) or Polygon (optional — overrides the sprite's own physics
        /// shape, which Unity derives automatically when a SpriteRenderer is present and Points is
        /// omitted): [[x,y], [x,y], ...].</summary>
        public float[][] Points { get; set; }

        public bool? AddRigidbody { get; set; }

        /// <summary>Adds a PlatformEffector2D-style flag — sets Collider2D.usedByEffector. The
        /// caller is responsible for adding the effector component itself (component/add).</summary>
        public bool? UsedByEffector { get; set; }
    }
}

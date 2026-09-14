namespace Mosaic.Bridge.Tools.Physics2D
{
    public sealed class Physics2DAddRigidbodyParams
    {
        public string Name { get; set; }
        public int? InstanceId { get; set; }

        /// <summary>"Dynamic" (default), "Kinematic", or "Static".</summary>
        public string BodyType { get; set; }

        public float? Mass { get; set; }
        public float? GravityScale { get; set; }
        public float? LinearDamping { get; set; }
        public float? AngularDamping { get; set; }

        /// <summary>Shorthand for FreezeRotation constraint — "in every platformer tutorial" per
        /// the course's own field report.</summary>
        public bool? FreezeRotation { get; set; }

        /// <summary>"None" (default), "Interpolate", or "Extrapolate".</summary>
        public string Interpolation { get; set; }

        /// <summary>"Discrete" (default) or "Continuous" — fast-moving bodies (bullets, a
        /// platformer's player at high speed) need Continuous to not tunnel through thin colliders.</summary>
        public string CollisionDetectionMode { get; set; }

        /// <summary>Asset path to a PhysicsMaterial2D.</summary>
        public string SharedMaterialPath { get; set; }
    }
}

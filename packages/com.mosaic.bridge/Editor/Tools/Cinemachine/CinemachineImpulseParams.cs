#if MOSAIC_HAS_CINEMACHINE
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Cinemachine
{
    public sealed class CinemachineImpulseParams
    {
        /// <summary>Action: add-source, add-collision-source.</summary>
        [Required] public string Action { get; set; }

        /// <summary>GameObject to add the impulse source component to — not necessarily a vcam
        /// (e.g. the object that lands/collides).</summary>
        [Required] public string TargetName { get; set; }

        /// <summary>Bump, Custom, Explosion, Recoil, Rumble. Null leaves Unity's default (Bump).</summary>
        public string ImpulseShape { get; set; }
        public float? ImpulseDuration { get; set; }
        public int? ImpulseChannel { get; set; }
        public float[] DefaultVelocity { get; set; }

        // -- add-collision-source --
        /// <summary>Layer names (comma-separated) that generate impulse events on collision.</summary>
        public string CollisionLayerMask { get; set; }
        public string IgnoreTag { get; set; }
        public bool? ScaleImpactWithSpeed { get; set; }
        public bool? ScaleImpactWithMass { get; set; }
        public bool? UseImpactDirection { get; set; }
    }
}
#endif

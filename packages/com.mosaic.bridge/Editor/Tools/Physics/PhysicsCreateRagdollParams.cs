using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsCreateRagdollParams
    {
        /// <summary>Root GameObject carrying a Humanoid Animator.</summary>
        public string Name { get; set; }
        public int? InstanceId { get; set; }

        /// <summary>Total ragdoll mass in kg, distributed across bones by body-part mass fraction. Default 20.</summary>
        public float TotalMass { get; set; } = 20f;

        /// <summary>0..1. Scales each CharacterJoint's twist/swing limit spring stiffness — higher
        /// holds the pose more rigidly (a "boxer" ragdoll) rather than going instantly limp. Default 0
        /// (fully limp, matching the classic ragdoll-wizard default).</summary>
        public float Strength { get; set; } = 0f;
    }
}

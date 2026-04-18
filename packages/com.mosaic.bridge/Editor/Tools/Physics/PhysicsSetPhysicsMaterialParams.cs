namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsSetPhysicsMaterialParams
    {
        /// <summary>Name of the target GameObject. Used if InstanceId is not set.</summary>
        public string Name { get; set; }
        /// <summary>Instance ID of the target GameObject. Takes priority over Name.</summary>
        public int? InstanceId { get; set; }
        /// <summary>Dynamic friction coefficient (0-1).</summary>
        public float DynamicFriction { get; set; }
        /// <summary>Static friction coefficient (0-1).</summary>
        public float StaticFriction { get; set; }
        /// <summary>Bounciness coefficient (0-1).</summary>
        public float Bounciness { get; set; }
        /// <summary>Optional asset path to save the PhysicMaterial (e.g., "Assets/MyMaterial.physicMaterial").</summary>
        public string AssetPath { get; set; }
    }
}

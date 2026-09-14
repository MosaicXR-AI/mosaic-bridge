namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsSetPhysicsMaterialParams
    {
        /// <summary>Name of the target GameObject. Used if InstanceId is not set.</summary>
        public string Name { get; set; }
        /// <summary>Instance ID of the target GameObject. Takes priority over Name.</summary>
        public int? InstanceId { get; set; }
        /// <summary>Dynamic friction coefficient (0-1). Omit to keep Unity's own PhysicsMaterial
        /// default of 0.6 — a plain 0 here used to mean "friction 0", an icy floor by accident.</summary>
        public float? DynamicFriction { get; set; }
        /// <summary>Static friction coefficient (0-1). Omit to keep Unity's own default of 0.6.</summary>
        public float? StaticFriction { get; set; }
        /// <summary>Bounciness coefficient (0-1). Omit to keep Unity's own default of 0.</summary>
        public float? Bounciness { get; set; }
        /// <summary>Optional asset path to save the PhysicsMaterial (e.g., "Assets/MyMaterial.physicMaterial").</summary>
        public string AssetPath { get; set; }
        /// <summary>When true and AssetPath already has a PhysicsMaterial asset, load and assign
        /// that existing asset (ignoring DynamicFriction/StaticFriction/Bounciness) instead of
        /// overwriting it with a brand-new one — lets multiple objects share one physics material.</summary>
        public bool ReuseExisting { get; set; }
    }
}

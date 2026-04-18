namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsAddRigidbodyParams
    {
        /// <summary>Name of the target GameObject. Used if InstanceId is not set.</summary>
        public string Name { get; set; }
        /// <summary>Instance ID of the target GameObject. Takes priority over Name.</summary>
        public int? InstanceId { get; set; }
        /// <summary>Mass of the Rigidbody in kilograms. Defaults to 1.</summary>
        public float? Mass { get; set; }
        /// <summary>Linear drag coefficient. Defaults to 0.</summary>
        public float? Drag { get; set; }
        /// <summary>Angular drag coefficient. Defaults to 0.05.</summary>
        public float? AngularDrag { get; set; }
        /// <summary>Whether gravity affects this Rigidbody. Defaults to true.</summary>
        public bool? UseGravity { get; set; }
        /// <summary>Whether this Rigidbody is kinematic. Defaults to false.</summary>
        public bool? IsKinematic { get; set; }
    }
}

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

        /// <summary>"None", "Interpolate", or "Extrapolate".</summary>
        public string Interpolation { get; set; }
        /// <summary>"Discrete", "Continuous", "ContinuousDynamic", or "ContinuousSpeculative".
        /// Use Continuous for fast-moving bodies that tunnel through thin colliders.</summary>
        public string CollisionDetection { get; set; }

        public bool? FreezePositionX { get; set; }
        public bool? FreezePositionY { get; set; }
        public bool? FreezePositionZ { get; set; }
        public bool? FreezeRotationX { get; set; }
        public bool? FreezeRotationY { get; set; }
        public bool? FreezeRotationZ { get; set; }

        /// <summary>[x,y,z] local-space center of mass override. Null keeps Unity's auto-computed value.</summary>
        public float[] CenterOfMass { get; set; }
        public float? MaxAngularVelocity { get; set; }

        /// <summary>Comma-separated layer names this body's colliders additionally include, overriding
        /// the Layer Collision Matrix.</summary>
        public string IncludeLayers { get; set; }
        /// <summary>Comma-separated layer names this body's colliders exclude.</summary>
        public string ExcludeLayers { get; set; }
    }
}

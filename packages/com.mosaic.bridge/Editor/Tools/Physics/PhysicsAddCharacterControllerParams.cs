namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsAddCharacterControllerParams
    {
        public string Name { get; set; }
        public int? InstanceId { get; set; }

        /// <summary>[x,y,z] capsule center offset. Null auto-fits from the GameObject's mesh/renderer bounds.</summary>
        public float[] Center { get; set; }
        /// <summary>Null auto-fits from the GameObject's mesh/renderer bounds.</summary>
        public float? Radius { get; set; }
        /// <summary>Null auto-fits from the GameObject's mesh/renderer bounds.</summary>
        public float? Height { get; set; }

        public float? SlopeLimit { get; set; }
        public float? StepOffset { get; set; }
        public float? SkinWidth { get; set; }
        public float? MinMoveDistance { get; set; }

        /// <summary>A Rigidbody on the same object conflicts with CharacterController's own movement
        /// (the classic beginner conflict). Default false refuses when one is present; set true to
        /// remove it instead.</summary>
        public bool RemoveExistingRigidbody { get; set; }
    }
}

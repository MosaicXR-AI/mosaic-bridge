namespace Mosaic.Bridge.Tools.Physics
{
    public sealed class PhysicsSimulateParams
    {
        /// <summary>Number of fixed steps to simulate.</summary>
        public int Steps { get; set; } = 1;

        /// <summary>Seconds per step. Null uses Time.fixedDeltaTime.</summary>
        public float? StepSize { get; set; }

        /// <summary>GameObject names whose Rigidbody should actually move. Every OTHER Rigidbody in
        /// the scene is temporarily forced kinematic for the duration of the simulation (restored
        /// afterward) — e.g. "drop crate A into the pit without also moving the player capsule."
        /// Null/empty means every Rigidbody in the scene simulates normally.</summary>
        public string[] Targets { get; set; }
    }
}

namespace Mosaic.Bridge.Tools.Physics
{
    /// <summary>
    /// Parameters for physics/spring-mass. Provide either MeshPath (asset path) or
    /// GameObjectName (a scene object with a MeshFilter) for surface topology; both
    /// are optional when Topology is "lattice".
    /// </summary>
    public sealed class PhysicsSpringMassParams
    {
        /// <summary>Asset path to a source mesh (e.g., "Assets/Models/Blob.fbx"). Optional.</summary>
        public string MeshPath { get; set; }

        /// <summary>Name of a scene GameObject with a MeshFilter. Optional.</summary>
        public string GameObjectName { get; set; }

        /// <summary>Preset: "jelly" (default), "cloth", "bounce", "hair".</summary>
        public string Preset { get; set; } = "jelly";

        /// <summary>Spring stiffness coefficient (N/m). Default 1000.</summary>
        public float SpringStiffness { get; set; } = 1000f;

        /// <summary>Damping coefficient. Default 5.</summary>
        public float Damping { get; set; } = 5f;

        /// <summary>Per-particle mass. Default 1.0.</summary>
        public float Mass { get; set; } = 1.0f;

        /// <summary>
        /// Topology: "surface" (vertex springs along mesh edges),
        /// "tetrahedral" (3D lattice with diagonals), "lattice" (axis-aligned grid).
        /// </summary>
        public string Topology { get; set; } = "surface";

        /// <summary>If greater than 0, springs will break when force magnitude exceeds this value. 0 = unbreakable.</summary>
        public float BreakForce { get; set; } = 0f;

        /// <summary>Optional custom system name. Defaults to a sanitized version of the source name.</summary>
        public string Name { get; set; }

        /// <summary>Optional world-space position applied to the instantiated GameObject.</summary>
        public float[] Position { get; set; }

        /// <summary>Output directory (Assets-relative). Defaults to "Assets/Generated/Physics/".</summary>
        public string SavePath { get; set; }
    }
}

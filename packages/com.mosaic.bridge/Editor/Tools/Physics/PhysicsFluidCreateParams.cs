namespace Mosaic.Bridge.Tools.Physics
{
    /// <summary>
    /// Parameters for <c>physics/fluid-create</c> — Jos Stam Stable Fluids (grid-based Eulerian Navier-Stokes).
    /// This tool generates a MonoBehaviour script implementing Stam's 1999 stable fluids solver
    /// and attaches it to a new GameObject in the scene. It is distinct from the SPH-based
    /// <c>simulation/fluid</c> tool (different algorithm, different use case).
    /// </summary>
    public sealed class PhysicsFluidCreateParams
    {
        /// <summary>Fluid type: "smoke", "liquid", or "fire". Defaults to "smoke".</summary>
        public string Type { get; set; }

        /// <summary>Grid resolution per axis. Clamped to [8, 128]. Defaults to 64.</summary>
        public int? Resolution { get; set; }

        /// <summary>Kinematic viscosity coefficient. Defaults to 0.0001.</summary>
        public float? Viscosity { get; set; }

        /// <summary>Scalar diffusion rate for density. Defaults to 0.0.</summary>
        public float? Diffusion { get; set; }

        /// <summary>Simulation fixed time step. Defaults to 0.1.</summary>
        public float? TimeStep { get; set; }

        /// <summary>Emitter position in normalized grid coordinates [0,1]^3. Defaults to [0.5, 0.5, 0.5].</summary>
        public float[] EmitterPosition { get; set; }

        /// <summary>Emitter radius in normalized grid coordinates. Defaults to 0.1.</summary>
        public float? EmitterRadius { get; set; }

        /// <summary>Emitter strength (amount of density / velocity injected per step). Defaults to 1.0.</summary>
        public float? EmitterStrength { get; set; }

        /// <summary>Whether to use a compute shader implementation. MVP is CPU-only — defaults to false.</summary>
        public bool? UseComputeShader { get; set; }

        /// <summary>Name of the spawned GameObject and the generated script. Optional.</summary>
        public string Name { get; set; }

        /// <summary>World position of the spawned GameObject. Optional — defaults to origin.</summary>
        public float[] Position { get; set; }

        /// <summary>Save path for generated script asset. Must start with "Assets/". Defaults to "Assets/Generated/Physics/".</summary>
        public string SavePath { get; set; }
    }
}

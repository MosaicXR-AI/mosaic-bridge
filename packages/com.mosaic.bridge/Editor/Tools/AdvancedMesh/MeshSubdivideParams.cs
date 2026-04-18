namespace Mosaic.Bridge.Tools.AdvancedMesh
{
    /// <summary>
    /// Parameters for mesh/subdivide. Provide either SourceMeshPath (asset path) or
    /// GameObjectName (a scene object with a MeshFilter). One of the two is required.
    /// </summary>
    public sealed class MeshSubdivideParams
    {
        /// <summary>Asset path to a source Mesh (e.g. "Assets/Meshes/foo.asset"). Optional.</summary>
        public string SourceMeshPath { get; set; }

        /// <summary>Name of a scene GameObject whose MeshFilter.sharedMesh will be used as source. Optional.</summary>
        public string GameObjectName { get; set; }

        /// <summary>Number of subdivision iterations. Clamped to [1, 4] — subdivision is exponential.</summary>
        public int Iterations { get; set; } = 1;

        /// <summary>Subdivision method: "catmull_clark", "loop", or "sqrt3".</summary>
        public string Method { get; set; } = "catmull_clark";

        /// <summary>If true, boundary edges/vertices are preserved (not smoothed inward).</summary>
        public bool PreserveCreases { get; set; } = true;

        /// <summary>Output mesh asset name (without extension). Defaults to source name + "_Subdiv".</summary>
        public string OutputName { get; set; }

        /// <summary>Folder to place generated mesh asset (Assets-relative).</summary>
        public string SavePath { get; set; } = "Assets/Generated/Mesh/";
    }
}

namespace Mosaic.Bridge.Tools.AdvancedMesh
{
    /// <summary>
    /// Parameters for mesh/convex-hull. Provide either SourceMeshPath (asset path) or
    /// GameObjectName (a scene object with a MeshFilter). One of the two is required.
    /// </summary>
    public sealed class MeshConvexHullParams
    {
        /// <summary>Asset path to a source mesh (e.g., "Assets/Models/Chair.fbx"). Optional.</summary>
        public string SourceMeshPath { get; set; }

        /// <summary>Name of a scene GameObject with a MeshFilter. Optional.</summary>
        public string GameObjectName { get; set; }

        /// <summary>Maximum hull vertices. Unity's convex MeshCollider limit is 255.</summary>
        public int MaxVertices { get; set; } = 255;

        /// <summary>If true and hull exceeds MaxVertices, simplify by removing least-contributing vertices.</summary>
        public bool Simplify { get; set; } = true;

        /// <summary>If true, attaches a convex MeshCollider to the source GameObject.</summary>
        public bool CreateCollider { get; set; } = false;

        /// <summary>If true, saves the hull as a .asset mesh via AssetDatabase.</summary>
        public bool CreateMesh { get; set; } = true;

        /// <summary>Output mesh name (without extension). Defaults to source name + "_Hull".</summary>
        public string OutputName { get; set; }

        /// <summary>Output directory (Assets-relative). Defaults to "Assets/Generated/Mesh/".</summary>
        public string SavePath { get; set; } = "Assets/Generated/Mesh/";
    }
}

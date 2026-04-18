namespace Mosaic.Bridge.Tools.AdvancedMesh
{
    public sealed class MeshDecimateParams
    {
        /// <summary>
        /// Asset path to a source Mesh (e.g. "Assets/Meshes/foo.asset"). Optional.
        /// One of SourceMeshPath or GameObjectName must be provided.
        /// </summary>
        public string SourceMeshPath { get; set; }

        /// <summary>
        /// Name of a scene GameObject whose MeshFilter.sharedMesh will be used as source.
        /// One of SourceMeshPath or GameObjectName must be provided.
        /// </summary>
        public string GameObjectName { get; set; }

        /// <summary>
        /// Target triangle count ratio. 1.0 = keep original, 0.1 = very simplified.
        /// Ignored when LodLevels is supplied.
        /// </summary>
        public float QualityRatio { get; set; } = 0.5f;

        public bool PreserveBoundary { get; set; } = true;
        public bool PreserveUVSeams { get; set; } = true;

        /// <summary>
        /// If true, a GameObject with a LODGroup is created referencing one mesh per LOD level.
        /// </summary>
        public bool GenerateLODGroup { get; set; } = false;

        /// <summary>
        /// Quality ratios for each LOD, e.g. [1.0, 0.5, 0.25, 0.1]. If null, a single mesh at
        /// QualityRatio is produced (or a default ladder when GenerateLODGroup is true).
        /// </summary>
        public float[] LodLevels { get; set; }

        /// <summary>Base name for generated mesh assets. Defaults to source mesh name + "_LOD".</summary>
        public string OutputName { get; set; }

        /// <summary>Folder in which to place generated mesh assets.</summary>
        public string SavePath { get; set; } = "Assets/Generated/Mesh/";
    }
}

namespace Mosaic.Bridge.Tools.AdvancedMesh
{
    public sealed class MeshDecimateResult
    {
        /// <summary>Asset paths of generated meshes, one per LOD level (ordered highest→lowest quality).</summary>
        public string[] MeshPaths { get; set; }

        /// <summary>Name of the created LODGroup GameObject when GenerateLODGroup is true.</summary>
        public string LodGroupGameObject { get; set; }

        public int OriginalTriangleCount { get; set; }
        public int[] DecimatedTriangleCounts { get; set; }

        public int OriginalVertexCount { get; set; }
        public int[] DecimatedVertexCounts { get; set; }

        public float[] QualityRatios { get; set; }
    }
}

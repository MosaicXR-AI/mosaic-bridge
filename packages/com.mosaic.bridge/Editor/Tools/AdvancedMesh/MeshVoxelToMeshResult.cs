namespace Mosaic.Bridge.Tools.AdvancedMesh
{
    public sealed class MeshVoxelToMeshResult
    {
        public string OutputPath { get; set; }
        public int VertexCount { get; set; }
        public int TriangleCount { get; set; }
        public string GridSize { get; set; }
        public float IsoLevel { get; set; }
        public int? GameObjectInstanceId { get; set; }
    }
}

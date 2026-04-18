namespace Mosaic.Bridge.Tools.AdvancedMesh
{
    public sealed class MeshGenerateResult
    {
        public string OutputPath { get; set; }
        public int VertexCount { get; set; }
        public int TriangleCount { get; set; }
        public bool HasUVs { get; set; }
        public bool HasNormals { get; set; }
        public int? GameObjectInstanceId { get; set; }
    }
}

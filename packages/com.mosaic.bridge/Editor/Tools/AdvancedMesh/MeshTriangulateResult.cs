namespace Mosaic.Bridge.Tools.AdvancedMesh
{
    public sealed class MeshTriangulateResult
    {
        public string OutputPath { get; set; }
        public int VertexCount { get; set; }
        public int TriangleCount { get; set; }
        public int? GameObjectInstanceId { get; set; }
    }
}

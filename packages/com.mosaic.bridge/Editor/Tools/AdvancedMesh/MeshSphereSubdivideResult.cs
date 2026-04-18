namespace Mosaic.Bridge.Tools.AdvancedMesh
{
    public sealed class MeshSphereSubdivideResult
    {
        public string OutputPath { get; set; }
        public int VertexCount { get; set; }
        public int TriangleCount { get; set; }
        public int Subdivisions { get; set; }
        public float Radius { get; set; }
        public int? GameObjectInstanceId { get; set; }
    }
}

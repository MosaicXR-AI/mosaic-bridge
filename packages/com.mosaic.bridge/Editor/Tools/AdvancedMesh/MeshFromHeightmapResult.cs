namespace Mosaic.Bridge.Tools.AdvancedMesh
{
    public sealed class MeshFromHeightmapResult
    {
        public string OutputPath { get; set; }
        public int VertexCount { get; set; }
        public int TriangleCount { get; set; }
        public float Width { get; set; }
        public float Depth { get; set; }
        public float HeightScale { get; set; }
        public int? GameObjectInstanceId { get; set; }
    }
}

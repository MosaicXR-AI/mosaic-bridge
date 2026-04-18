namespace Mosaic.Bridge.Tools.AdvancedMesh
{
    public sealed class MeshDualContourResult
    {
        public string MeshPath { get; set; }
        public string GameObjectName { get; set; }
        public int VertexCount { get; set; }
        public int TriangleCount { get; set; }
        public int Resolution { get; set; }
        public string SdfFunction { get; set; }
    }
}

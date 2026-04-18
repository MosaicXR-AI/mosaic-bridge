namespace Mosaic.Bridge.Tools.AdvancedMesh
{
    public sealed class MeshSubdivideResult
    {
        public string MeshPath { get; set; }
        public string Method { get; set; }
        public int Iterations { get; set; }
        public int OriginalVertexCount { get; set; }
        public int OriginalTriangleCount { get; set; }
        public int NewVertexCount { get; set; }
        public int NewTriangleCount { get; set; }
    }
}

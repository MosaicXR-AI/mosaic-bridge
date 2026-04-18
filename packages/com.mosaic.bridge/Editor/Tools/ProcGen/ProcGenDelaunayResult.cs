namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenDelaunayResult
    {
        public int    TriangleCount  { get; set; }
        public int    VertexCount    { get; set; }
        public int[]  Triangles      { get; set; }
        public string MeshPath       { get; set; }
        public string GameObjectName { get; set; }
    }
}

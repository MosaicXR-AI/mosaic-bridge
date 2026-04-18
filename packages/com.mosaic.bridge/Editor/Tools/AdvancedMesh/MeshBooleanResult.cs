namespace Mosaic.Bridge.Tools.AdvancedMesh
{
    public sealed class MeshBooleanResult
    {
        public string MeshPath { get; set; }
        public string GameObjectName { get; set; }
        public int InstanceId { get; set; }
        public string Operation { get; set; }
        public int VertexCount { get; set; }
        public int TriangleCount { get; set; }
        public bool OriginalsKept { get; set; }
    }
}

namespace Mosaic.Bridge.Tools.AdvancedMesh
{
    public sealed class MeshConvexHullResult
    {
        public string MeshPath { get; set; }
        public string GameObjectName { get; set; }
        public int OriginalVertexCount { get; set; }
        public int HullVertexCount { get; set; }
        public int HullTriangleCount { get; set; }
        public bool ColliderAdded { get; set; }
    }
}

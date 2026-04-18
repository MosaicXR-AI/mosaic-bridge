namespace Mosaic.Bridge.Tools.AdvancedMesh
{
    public sealed class MeshVoxelToMeshParams
    {
        public int GridSizeX { get; set; } = 16;
        public int GridSizeY { get; set; } = 16;
        public int GridSizeZ { get; set; } = 16;
        public float IsoLevel { get; set; } = 0.5f;
        public float[] VoxelData { get; set; }
        public string OutputPath { get; set; } = "Assets/Generated/Meshes/voxel_mesh.asset";
        public bool CreateGameObject { get; set; } = true;
    }
}

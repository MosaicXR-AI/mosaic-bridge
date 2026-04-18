using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.AdvancedMesh
{
    public sealed class MeshFromHeightmapParams
    {
        [Required] public string HeightmapPath { get; set; }
        public float Width { get; set; } = 100f;
        public float Depth { get; set; } = 100f;
        public float HeightScale { get; set; } = 10f;
        public int Resolution { get; set; } = 256;
        public string OutputPath { get; set; } = "Assets/Generated/Meshes/heightmap_mesh.asset";
        public bool CreateGameObject { get; set; } = true;
    }
}

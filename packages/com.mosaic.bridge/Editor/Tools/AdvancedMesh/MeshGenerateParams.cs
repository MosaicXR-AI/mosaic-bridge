using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.AdvancedMesh
{
    public sealed class MeshGenerateParams
    {
        [Required] public float[] Vertices { get; set; }
        [Required] public int[] Triangles { get; set; }
        public float[] UVs { get; set; }
        public float[] Normals { get; set; }
        public string OutputPath { get; set; } = "Assets/Generated/Meshes/generated.asset";
        public bool CreateGameObject { get; set; } = true;
    }
}

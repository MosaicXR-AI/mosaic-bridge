using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.AdvancedMesh
{
    public sealed class MeshTriangulateParams
    {
        [Required] public float[] Points { get; set; }
        public string OutputPath { get; set; } = "Assets/Generated/Meshes/triangulated.asset";
        public bool CreateGameObject { get; set; } = true;
    }
}

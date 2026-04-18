using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.AdvancedMesh
{
    public sealed class MeshSphereSubdivideParams
    {
        [Required] public int Subdivisions { get; set; }
        public float Radius { get; set; } = 1f;
        public string OutputPath { get; set; } = "Assets/Generated/Meshes/icosphere.asset";
        public bool CreateGameObject { get; set; } = true;
    }
}

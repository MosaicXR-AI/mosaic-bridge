using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.AdvancedMesh
{
    public sealed class MeshBooleanParams
    {
        /// <summary>GameObject name for operand A (must have MeshFilter with sharedMesh).</summary>
        [Required] public string MeshAGameObject { get; set; }

        /// <summary>GameObject name for operand B (must have MeshFilter with sharedMesh).</summary>
        [Required] public string MeshBGameObject { get; set; }

        /// <summary>Operation: "union", "subtract", or "intersect".</summary>
        [Required] public string Operation { get; set; }

        /// <summary>If false (default), deletes the source A and B GameObjects after the boolean.</summary>
        public bool KeepOriginals { get; set; } = false;

        /// <summary>If true, adds a MeshCollider to the output GameObject.</summary>
        public bool GenerateCollider { get; set; } = false;

        /// <summary>Optional output GameObject name. Defaults to "{A}_{Op}_{B}".</summary>
        public string OutputName { get; set; }

        /// <summary>Folder where the result .asset is saved. Default "Assets/Generated/Mesh/".</summary>
        public string SavePath { get; set; } = "Assets/Generated/Mesh/";
    }
}

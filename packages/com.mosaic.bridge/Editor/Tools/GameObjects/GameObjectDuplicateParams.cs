using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.GameObjects
{
    public sealed class GameObjectDuplicateParams
    {
        /// <summary>Name of the source GameObject to duplicate.</summary>
        [Required] public string Name { get; set; }

        /// <summary>Optional override name for the duplicate. When null, a unique
        /// name derived from the source (e.g. "Cube (1)") is used. Pass explicitly
        /// when you need deterministic names (e.g., "Cube_0", "Cube_1").</summary>
        public string NewName { get; set; }

        /// <summary>Optional world-space position for the duplicate. When null,
        /// the duplicate spawns at the source's exact transform.</summary>
        public float[] Position { get; set; }

        /// <summary>Optional parent GameObject name. When null, the duplicate's
        /// parent matches the source's parent. Pass empty string to unparent.</summary>
        public string Parent { get; set; }
    }
}

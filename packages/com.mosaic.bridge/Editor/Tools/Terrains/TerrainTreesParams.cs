using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Terrains
{
    public sealed class TerrainTreesParams
    {
        public int    InstanceId { get; set; }
        public string Name       { get; set; }

        [Required] public string Action { get; set; } // add-prototype, place, clear, get-instances

        /// <summary>Prefab asset path for add-prototype (e.g. "Assets/Prefabs/Tree.prefab").</summary>
        public string PrefabPath { get; set; }

        /// <summary>Tree prototype index for place action.</summary>
        public int PrototypeIndex { get; set; }

        /// <summary>Normalized position [x,y,z] on the terrain (0..1 for x/z) for place.</summary>
        public float[] Position { get; set; }

        /// <summary>Width scale for placed tree.</summary>
        public float WidthScale { get; set; } = 1f;

        /// <summary>Height scale for placed tree.</summary>
        public float HeightScale { get; set; } = 1f;

        /// <summary>Number of random trees to place (for batch placement).</summary>
        public int Count { get; set; } = 1;

        /// <summary>Random seed for batch placement.</summary>
        public int Seed { get; set; } = 0;
    }
}

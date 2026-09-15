using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Terrains
{
    public sealed class TerrainTreesParams
    {
        public int    InstanceId { get; set; }
        public string Name       { get; set; }

        [Required] public string Action { get; set; } // add-prototype, place, place-list, clear, get-instances

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

        // -- place (masked scatter, avoids roads/water/cliffs) --

        /// <summary>Degrees, inclusive. Candidates outside this range are re-rolled.</summary>
        public float? MinSlope { get; set; }
        public float? MaxSlope { get; set; }
        /// <summary>World-space height, inclusive.</summary>
        public float? MinHeight { get; set; }
        public float? MaxHeight { get; set; }
        /// <summary>Splatmap layer index a candidate must be painted with (weight >= MinLayerWeight)
        /// to be accepted — e.g. only scatter trees on the "grass" layer, not "road" or "water".</summary>
        public int? RequiredLayerIndex { get; set; }
        public float MinLayerWeight { get; set; } = 0.5f;
        /// <summary>Cap on candidate rolls before giving up (partial placement is still returned).
        /// Defaults to Count*20.</summary>
        public int? MaxAttempts { get; set; }

        // -- place-list --

        /// <summary>Explicit positions, flat normalized [x,y,z, x,y,z, ...] (0..1 for x/z; y is
        /// ignored and recomputed from the heightmap).</summary>
        public float[] Positions { get; set; }
        /// <summary>Per-tree rotation in degrees, parallel to Positions. Omit for all-zero.</summary>
        public float[] Rotations { get; set; }
        /// <summary>Per-tree color, flat [r,g,b, r,g,b, ...], parallel to Positions. Omit for white.</summary>
        public float[] Colors { get; set; }
    }
}

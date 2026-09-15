using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Terrains
{
    public sealed class TerrainDetailParams
    {
        public int    InstanceId { get; set; }
        public string Name       { get; set; }

        [Required] public string Action { get; set; } // add-prototype, paint, scatter, clear, set-resolution, scatter-mode

        /// <summary>Texture asset path for grass detail prototype.</summary>
        public string TexturePath { get; set; }

        /// <summary>Prefab path for mesh detail prototype.</summary>
        public string PrefabPath { get; set; }

        /// <summary>Detail prototype index for paint/scatter.</summary>
        public int PrototypeIndex { get; set; }

        /// <summary>Normalized X position (0..1) for paint.</summary>
        public float X { get; set; } = 0.5f;

        /// <summary>Normalized Y position (0..1) for paint.</summary>
        public float Y { get; set; } = 0.5f;

        /// <summary>Brush radius in detail resolution samples.</summary>
        public int Radius { get; set; } = 10;

        /// <summary>Detail density value (0..16 per cell).</summary>
        public int Density { get; set; } = 8;

        /// <summary>Random seed for scatter.</summary>
        public int Seed { get; set; } = 0;

        /// <summary>Min width for detail prototype.</summary>
        public float MinWidth { get; set; } = 1f;

        /// <summary>Max width for detail prototype.</summary>
        public float MaxWidth { get; set; } = 2f;

        /// <summary>Min height for detail prototype.</summary>
        public float MinHeight { get; set; } = 0.5f;

        /// <summary>Max height for detail prototype.</summary>
        public float MaxHeight { get; set; } = 1.5f;

        // -- add-prototype: full config --

        /// <summary>"GrassBillboard", "VertexLit", or "Grass". Null leaves Unity's default.</summary>
        public string RenderMode { get; set; }
        public bool? UseInstancing { get; set; }
        /// <summary>[r,g,b] or [r,g,b,a], 0..1.</summary>
        public float[] HealthyColor { get; set; }
        public float[] DryColor { get; set; }
        public float? NoiseSpread { get; set; }
        /// <summary>0..1 blend toward aligning with terrain normal (Unity's alignToGround is a float, not a toggle).</summary>
        public float? AlignToGround { get; set; }

        // -- set-resolution --
        public int DetailResolution { get; set; }
        public int ResolutionPerPatch { get; set; } = 16;

        // -- scatter-mode --
        /// <summary>"CoverageMode" or "InstanceCountMode".</summary>
        public string ScatterMode { get; set; }

        // -- scatter: tunable coverage + masked scatter --

        /// <summary>Fraction (0..1) of samples that receive Density. Default 0.3 (the prior hardcoded value).</summary>
        public float ScatterCoverage { get; set; } = 0.3f;
        public float? MinSlope { get; set; }
        public float? MaxSlope { get; set; }
        public float? MinHeightWorld { get; set; }
        public float? MaxHeightWorld { get; set; }
        public int? RequiredLayerIndex { get; set; }
        public float MinLayerWeight { get; set; } = 0.5f;
    }
}

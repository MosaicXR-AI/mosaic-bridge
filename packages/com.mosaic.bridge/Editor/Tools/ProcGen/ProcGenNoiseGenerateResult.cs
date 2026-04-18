namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenNoiseGenerateResult
    {
        /// <summary>Path to saved texture asset (null if output != "texture").</summary>
        public string  TexturePath     { get; set; }

        /// <summary>Resolution used for generation.</summary>
        public int     Resolution      { get; set; }

        /// <summary>Noise algorithm that was used.</summary>
        public string  NoiseType       { get; set; }

        /// <summary>Fractal combine mode that was used.</summary>
        public string  CombineMode     { get; set; }

        /// <summary>Value range of the generated noise before normalization.</summary>
        public NoiseRange Range        { get; set; }

        /// <summary>Whether the heightmap was applied to a terrain.</summary>
        public bool    HeightmapApplied { get; set; }

        /// <summary>Name of the terrain the heightmap was applied to (null if none).</summary>
        public string  TerrainName     { get; set; }
    }

    public sealed class NoiseRange
    {
        public float Min { get; set; }
        public float Max { get; set; }
    }
}

namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenNoiseGenerateParams
    {
        /// <summary>Noise algorithm: "simplex", "perlin", "cellular", "value".</summary>
        public string  NoiseType      { get; set; }

        /// <summary>Square texture resolution (width = height). Default 256.</summary>
        public int?    Resolution     { get; set; }

        /// <summary>Base noise frequency. Default 1.0.</summary>
        public float?  Frequency      { get; set; }

        /// <summary>Number of fractal octaves. Default 4.</summary>
        public int?    Octaves        { get; set; }

        /// <summary>Frequency multiplier per octave. Default 2.0.</summary>
        public float?  Lacunarity     { get; set; }

        /// <summary>Amplitude multiplier per octave. Default 0.5.</summary>
        public float?  Persistence    { get; set; }

        /// <summary>Random seed for deterministic output. Random if null.</summary>
        public int?    Seed           { get; set; }

        /// <summary>Fractal combine mode: "fbm", "ridged", "turbulence", "billow". Default "fbm".</summary>
        public string  CombineMode    { get; set; }

        /// <summary>Output format: "texture", "heightmap", "float_array". Default "texture".</summary>
        public string  Output         { get; set; }

        /// <summary>If set, apply generated noise as heightmap to this terrain GameObject.</summary>
        public string  ApplyToTerrain { get; set; }

        /// <summary>Save path for generated texture. Default "Assets/Generated/Noise/".</summary>
        public string  SavePath       { get; set; }
    }
}

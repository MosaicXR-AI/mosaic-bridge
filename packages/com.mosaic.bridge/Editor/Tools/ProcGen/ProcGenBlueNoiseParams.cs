namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenBlueNoiseParams
    {
        /// <summary>Square texture resolution (width = height). Default 256.</summary>
        public int?    Resolution { get; set; }

        /// <summary>Number of texture channels (1-4). Default 1.</summary>
        public int?    Channels   { get; set; }

        /// <summary>Whether the texture tiles seamlessly (toroidal distance). Default true.</summary>
        public bool?   Tiling     { get; set; }

        /// <summary>Output mode: "texture" or "points". Default "texture".</summary>
        public string  Output     { get; set; }

        /// <summary>Number of points to generate (required for "points" output).</summary>
        public int?    PointCount { get; set; }

        /// <summary>Minimum bounds for point generation [x, y]. Optional.</summary>
        public float[] BoundsMin  { get; set; }

        /// <summary>Maximum bounds for point generation [x, y]. Optional.</summary>
        public float[] BoundsMax  { get; set; }

        /// <summary>Random seed for deterministic output. Random if null.</summary>
        public int?    Seed       { get; set; }

        /// <summary>Save path for generated texture. Default "Assets/Generated/BlueNoise/".</summary>
        public string  SavePath   { get; set; }
    }
}

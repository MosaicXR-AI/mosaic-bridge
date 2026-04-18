namespace Mosaic.Bridge.Tools.ProcGen
{
    public sealed class ProcGenBlueNoiseResult
    {
        /// <summary>Path to saved texture asset (null if output != "texture").</summary>
        public string    TexturePath { get; set; }

        /// <summary>Resolution used for generation.</summary>
        public int       Resolution  { get; set; }

        /// <summary>Number of channels in the generated texture.</summary>
        public int       Channels    { get; set; }

        /// <summary>Whether the texture was generated with seamless tiling.</summary>
        public bool      Tiling      { get; set; }

        /// <summary>Generated point positions as [x, y] pairs (null if output != "points").</summary>
        public float[][] Points      { get; set; }

        /// <summary>Number of generated points.</summary>
        public int       PointCount  { get; set; }
    }
}

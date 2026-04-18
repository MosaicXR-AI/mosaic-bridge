using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.AdvancedRendering
{
    public sealed class ShaderCreateRaymarcherParams
    {
        public string[] Primitives     { get; set; }
        public string   Operation      { get; set; }
        public float?   SmoothFactor   { get; set; }
        public int?     MaxSteps       { get; set; }
        public float?   MaxDistance     { get; set; }
        public float?   SurfaceDistance { get; set; }
        public string   OutputDirectory { get; set; }
    }
}

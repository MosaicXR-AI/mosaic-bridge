namespace Mosaic.Bridge.Tools.AdvancedRendering
{
    public sealed class RenderAtmosphereParams
    {
        public float?   PlanetRadius          { get; set; }
        public float?   AtmosphereHeight      { get; set; }
        public float?   RayleighScaleHeight   { get; set; }
        public float?   MieScaleHeight        { get; set; }
        public float?   SunIntensity          { get; set; }
        public float[]  RayleighCoefficients  { get; set; }
        public float?   MieCoefficient        { get; set; }
        public string   OutputDirectory       { get; set; }
    }
}

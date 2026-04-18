namespace Mosaic.Bridge.Tools.AdvancedRendering
{
    public sealed class RenderVolumetricCloudsParams
    {
        public float?  CloudDensity      { get; set; }
        public float[] WindDirection     { get; set; }
        public float?  WindSpeed         { get; set; }
        public float?  LightAbsorption   { get; set; }
        public float?  DetailNoiseScale  { get; set; }
        public float?  ShapeNoiseScale   { get; set; }
        public float?  CloudMinHeight    { get; set; }
        public float?  CloudMaxHeight    { get; set; }
        public int?    RaySteps          { get; set; }
        public int?    LightSteps        { get; set; }
        public string  OutputDirectory   { get; set; }
    }
}

namespace Mosaic.Bridge.Tools.AdvancedRendering
{
    public sealed class RenderingVolumetricFogParams
    {
        public string  Pipeline              { get; set; }
        public int[]   Resolution            { get; set; }
        public float?  Density               { get; set; }
        public float?  Scattering            { get; set; }
        public float?  Extinction            { get; set; }
        public float?  MaxDistance           { get; set; }
        public bool?   TemporalReprojection  { get; set; }
        public float[] FogColor              { get; set; }
        public string  Name                  { get; set; }
        public string  SavePath              { get; set; }
    }
}

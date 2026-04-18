namespace Mosaic.Bridge.Tools.AdvancedRendering
{
    public sealed class RenderingRadianceCascadesParams
    {
        public int?    CascadeCount  { get; set; }
        public float?  ProbeSpacing  { get; set; }
        public int?    BounceCount   { get; set; }
        public float?  Intensity     { get; set; }
        public string  Pipeline      { get; set; }
        public int[]   Resolution    { get; set; }
        public string  OutputName    { get; set; }
        public string  SavePath      { get; set; }
    }
}

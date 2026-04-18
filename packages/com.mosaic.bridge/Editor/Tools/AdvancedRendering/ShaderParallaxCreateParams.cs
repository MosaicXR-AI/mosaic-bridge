namespace Mosaic.Bridge.Tools.AdvancedRendering
{
    public sealed class ShaderParallaxCreateParams
    {
        public string HeightmapPath { get; set; }
        public string AlbedoPath    { get; set; }
        public string NormalPath    { get; set; }
        public float? HeightScale   { get; set; }
        public int?   MinSteps      { get; set; }
        public int?   MaxSteps      { get; set; }
        public bool?  SelfShadow    { get; set; }
        public string Pipeline      { get; set; }
        public string OutputName    { get; set; }
        public string SavePath      { get; set; }
    }
}

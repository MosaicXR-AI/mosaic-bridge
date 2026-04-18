namespace Mosaic.Bridge.Tools.AdvancedRendering
{
    public sealed class ShaderCreateRaymarcherResult
    {
        public string   ShaderPath   { get; set; }
        public string   MaterialPath { get; set; }
        public string[] Primitives   { get; set; }
        public string   Operation    { get; set; }
    }
}

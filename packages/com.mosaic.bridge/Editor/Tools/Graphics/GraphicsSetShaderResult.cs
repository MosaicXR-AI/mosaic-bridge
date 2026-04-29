namespace Mosaic.Bridge.Tools.Graphics
{
    public sealed class GraphicsSetShaderResult
    {
        public string MaterialPath           { get; set; }
        public string MaterialName           { get; set; }
        public string ShaderName             { get; set; }
        public string PreviousShaderName     { get; set; }
        public string SuggestedColorProperty { get; set; }
    }
}

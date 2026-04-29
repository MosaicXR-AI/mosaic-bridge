namespace Mosaic.Bridge.Tools.Materials
{
    public sealed class MaterialCreateResult
    {
        public string Path                  { get; set; }
        public string ShaderName            { get; set; }
        public string Guid                  { get; set; }
        public bool   Overwritten           { get; set; }
        public string RenderPipeline        { get; set; }
        public string SuggestedColorProperty { get; set; }
    }
}

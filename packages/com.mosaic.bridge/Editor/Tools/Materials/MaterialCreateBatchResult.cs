namespace Mosaic.Bridge.Tools.Materials
{
    public sealed class MaterialCreateBatchResult
    {
        public MaterialCreateBatchResultEntry[] Created  { get; set; }
        public MaterialCreateBatchResultEntry[] Skipped  { get; set; }
        public MaterialCreateBatchResultEntry[] Failed   { get; set; }
        public string                           RenderPipeline         { get; set; }
        public string                           SuggestedColorProperty { get; set; }
    }

    public sealed class MaterialCreateBatchResultEntry
    {
        public string Path       { get; set; }
        public string ShaderName { get; set; }
        public string Error      { get; set; }
    }
}

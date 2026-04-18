namespace Mosaic.Bridge.Tools.ShaderGraphs
{
    public sealed class ShaderGraphSetPropertyDefaultResult
    {
        public string AssetPath    { get; set; }
        public string PropertyName { get; set; }
        public string OldValue     { get; set; }
        public string NewValue     { get; set; }
    }
}

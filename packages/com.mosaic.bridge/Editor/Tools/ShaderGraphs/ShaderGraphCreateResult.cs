namespace Mosaic.Bridge.Tools.ShaderGraphs
{
    public sealed class ShaderGraphCreateResult
    {
        public string Path        { get; set; }
        public string Name        { get; set; }
        public string ShaderType  { get; set; }
        public string Guid        { get; set; }
        public bool   Overwritten { get; set; }
    }
}

namespace Mosaic.Bridge.Tools.Materials
{
    public sealed class MaterialCreateParams
    {
        public string Path             { get; set; }
        public string ShaderName       { get; set; } = null;
        public bool   OverwriteExisting { get; set; } = false;
    }
}

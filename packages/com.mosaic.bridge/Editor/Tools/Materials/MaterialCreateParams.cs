namespace Mosaic.Bridge.Tools.Materials
{
    public sealed class MaterialCreateParams
    {
        public string Path             { get; set; }
        public string ShaderName       { get; set; } = "Standard";
        public bool   OverwriteExisting { get; set; } = false;
    }
}

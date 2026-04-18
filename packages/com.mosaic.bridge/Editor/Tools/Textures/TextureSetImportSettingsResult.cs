namespace Mosaic.Bridge.Tools.Textures
{
    public sealed class TextureSetImportSettingsResult
    {
        public string AssetPath { get; set; }
        public string TextureType { get; set; }
        public int MaxSize { get; set; }
        public string Compression { get; set; }
        public bool SRGB { get; set; }
        public string FilterMode { get; set; }
        public string WrapMode { get; set; }
    }
}

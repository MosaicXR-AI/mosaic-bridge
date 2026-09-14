namespace Mosaic.Bridge.Tools.Textures
{
    public sealed class TextureSetImportSettingsResult
    {
        public string AssetPath { get; set; }
        public string TextureType { get; set; }
        public string TextureShape { get; set; }
        public int MaxSize { get; set; }
        public string Compression { get; set; }
        public bool SRGB { get; set; }
        public string FilterMode { get; set; }
        public string WrapMode { get; set; }
        public string SpriteMode { get; set; }
        public float PixelsPerUnit { get; set; }
        public float[] Pivot { get; set; }
        public float[] Border { get; set; }
        public string MeshType { get; set; }
    }
}

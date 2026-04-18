using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Textures
{
    public sealed class TextureSetImportSettingsParams
    {
        /// <summary>Asset path to the texture (e.g., "Assets/Textures/MyTex.png").</summary>
        [Required] public string AssetPath { get; set; }
        /// <summary>Texture type: Default, NormalMap, Sprite, or Editor. Null to leave unchanged.</summary>
        public string TextureType { get; set; }
        /// <summary>Maximum texture size (32, 64, 128, 256, 512, 1024, 2048, 4096, 8192). Null to leave unchanged.</summary>
        public int? MaxSize { get; set; }
        /// <summary>Compression quality: None, LowQuality, NormalQuality, HighQuality. Null to leave unchanged.</summary>
        public string Compression { get; set; }
        /// <summary>Whether the texture uses sRGB color space. Null to leave unchanged.</summary>
        public bool? SRGB { get; set; }
        /// <summary>Filter mode: Point, Bilinear, Trilinear. Null to leave unchanged.</summary>
        public string FilterMode { get; set; }
        /// <summary>Wrap mode: Repeat, Clamp, Mirror, MirrorOnce. Null to leave unchanged.</summary>
        public string WrapMode { get; set; }
    }
}

using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Sprites
{
    public sealed class SpriteInfoParams
    {
        /// <summary>Path to a texture/sprite sheet asset, e.g. "Assets/sheet.png". Lists every
        /// sub-sprite (a sliced sheet) or the single sprite (Single import mode).</summary>
        [Required] public string AssetPath { get; set; }
    }
}

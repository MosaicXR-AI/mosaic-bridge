using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioCreateMixerParams
    {
        /// <summary>Where to create the mixer, e.g. "Assets/Audio/Main.mixer". Must end in ".mixer".</summary>
        [Required] public string AssetPath { get; set; }
    }
}

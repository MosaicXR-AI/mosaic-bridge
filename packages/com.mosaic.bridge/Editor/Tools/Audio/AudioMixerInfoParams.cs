using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioMixerInfoParams
    {
        [Required] public string MixerAssetPath { get; set; }
    }
}

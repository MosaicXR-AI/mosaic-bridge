using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioClipInfoParams
    {
        /// <summary>Asset path to the audio clip (e.g., "Assets/Audio/Footstep.wav").</summary>
        [Required] public string AssetPath { get; set; }

        /// <summary>Platform tab to report override settings for (default "Standalone").</summary>
        public string Platform { get; set; } = "Standalone";
    }
}

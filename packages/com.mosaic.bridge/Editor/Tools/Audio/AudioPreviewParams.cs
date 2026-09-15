using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioPreviewParams
    {
        /// <summary>"play", "stop", or "status".</summary>
        [Required] public string Action { get; set; }

        /// <summary>Asset path of the clip to play (required for "play").</summary>
        public string ClipPath { get; set; }

        /// <summary>Sample offset to start playback from.</summary>
        public int StartSample { get; set; } = 0;

        /// <summary>Whether the preview loops.</summary>
        public bool Loop { get; set; } = false;
    }
}

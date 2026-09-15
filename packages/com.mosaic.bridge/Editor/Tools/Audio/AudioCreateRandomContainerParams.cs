using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioCreateRandomContainerParams
    {
        /// <summary>Asset path to create the container at (e.g. "Assets/Audio/Footsteps.asset").</summary>
        [Required] public string AssetPath { get; set; }

        /// <summary>Asset paths of the AudioClips to add as elements, played back randomly.</summary>
        [Required] public string[] ClipPaths { get; set; }

        /// <summary>Per-element volume in dB, parallel to ClipPaths. Null/short entries default to 0dB.</summary>
        public float[] ElementVolumes { get; set; }

        /// <summary>Playback mode, e.g. "Sequential" or "Random" (exact members read from the
        /// installed Unity version — see the failure message for valid names if unsure).</summary>
        public string PlaybackMode { get; set; }

        /// <summary>Trigger mode, e.g. "OnDemand" or "Automatic" (exact members read from the
        /// installed Unity version — see the failure message for valid names if unsure).</summary>
        public string TriggerMode { get; set; }

        /// <summary>[min, max] volume randomization range in dB applied per-play. Enables
        /// randomization when set.</summary>
        public float[] VolumeRandomizationRange { get; set; }
    }
}

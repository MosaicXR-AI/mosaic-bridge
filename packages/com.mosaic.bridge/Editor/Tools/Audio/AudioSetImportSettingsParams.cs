using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioSetImportSettingsParams
    {
        /// <summary>Asset path to the audio clip (e.g., "Assets/Audio/Footstep.wav").</summary>
        [Required] public string AssetPath { get; set; }

        /// <summary>Force the clip to mono. Null to leave unchanged.</summary>
        public bool? ForceToMono { get; set; }

        /// <summary>Load the clip data in the background instead of blocking the main thread. Null to leave unchanged.</summary>
        public bool? LoadInBackground { get; set; }

        /// <summary>Treat the clip as ambisonic. Null to leave unchanged.</summary>
        public bool? Ambisonic { get; set; }

        /// <summary>Platform tab to write per-platform overrides to (e.g. "Standalone", "Android", "iOS", "WebGL").
        /// Defaults to "Standalone". Ignored if none of LoadType/CompressionFormat/SampleRateSetting/
        /// SampleRateOverride/Quality/PreloadAudioData are set and ClearOverride is not requested.</summary>
        public string Platform { get; set; } = "Standalone";

        /// <summary>Removes the per-platform override for Platform, reverting it to the default settings. Applied before any other override field.</summary>
        public bool? ClearOverride { get; set; }

        /// <summary>Load type: "DecompressOnLoad", "CompressedInMemory", or "Streaming". Null to leave unchanged.</summary>
        public string LoadType { get; set; }

        /// <summary>Compression format: "PCM", "Vorbis", "ADPCM", "MP3", "AAC", or another AudioCompressionFormat member name. Null to leave unchanged.</summary>
        public string CompressionFormat { get; set; }

        /// <summary>Sample rate handling: "PreserveSampleRate", "OptimizeSampleRate", or "OverrideSampleRate". Null to leave unchanged.</summary>
        public string SampleRateSetting { get; set; }

        /// <summary>Target sample rate in Hz when SampleRateSetting is "OverrideSampleRate". Null to leave unchanged.</summary>
        public uint? SampleRateOverride { get; set; }

        /// <summary>Compression quality, 0..1. Null to leave unchanged.</summary>
        public float? Quality { get; set; }

        /// <summary>Preload audio data when the clip asset loads. Null to leave unchanged.</summary>
        public bool? PreloadAudioData { get; set; }
    }
}

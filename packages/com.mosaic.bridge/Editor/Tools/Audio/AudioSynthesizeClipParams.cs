using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioSynthesizeClipParams
    {
        /// <summary>Project-relative path to write the generated clip to (e.g. "Assets/Audio/Footstep.wav").</summary>
        [Required] public string AssetPath { get; set; }

        /// <summary>Waveform: "tone" (sine), "noise" (white), "sweep" (linear chirp from Frequency to EndFrequency), or "click" (single-sample impulse + short decay).</summary>
        [Required] public string Waveform { get; set; }

        /// <summary>Clip duration in seconds.</summary>
        public float DurationSeconds { get; set; } = 1f;

        /// <summary>Tone/sweep-start frequency in Hz.</summary>
        public float Frequency { get; set; } = 440f;

        /// <summary>Sweep-end frequency in Hz (sweep only).</summary>
        public float EndFrequency { get; set; } = 880f;

        /// <summary>Output sample rate in Hz.</summary>
        public int SampleRate { get; set; } = 44100;

        /// <summary>Peak amplitude, 0..1.</summary>
        public float Amplitude { get; set; } = 0.5f;

        /// <summary>Channel count (1 = mono, 2 = stereo — both channels identical).</summary>
        public int Channels { get; set; } = 1;

        /// <summary>Random seed for "noise" (deterministic output for the same seed).</summary>
        public int Seed { get; set; } = 0;
    }
}

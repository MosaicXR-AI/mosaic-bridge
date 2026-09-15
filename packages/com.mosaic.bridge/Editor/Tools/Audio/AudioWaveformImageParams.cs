using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioWaveformImageParams
    {
        /// <summary>Asset path to the source audio clip.</summary>
        [Required] public string AssetPath { get; set; }

        /// <summary>Project-relative path to write the generated PNG to (e.g. "Assets/Audio/Footstep_Waveform.png").</summary>
        [Required] public string OutputPath { get; set; }

        /// <summary>Image width in pixels (one min/max sample bucket per column).</summary>
        public int Width { get; set; } = 512;

        /// <summary>Image height in pixels.</summary>
        public int Height { get; set; } = 128;

        /// <summary>Waveform line color as [r,g,b] or [r,g,b,a], 0..1. Defaults to a Unity-inspector-like green.</summary>
        public float[] WaveformColor { get; set; }

        /// <summary>Background color as [r,g,b] or [r,g,b,a], 0..1. Defaults to transparent black.</summary>
        public float[] BackgroundColor { get; set; }
    }
}

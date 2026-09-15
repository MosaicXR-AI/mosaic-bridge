namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioSynthesizeClipResult
    {
        public string AssetPath       { get; set; }
        public string Waveform        { get; set; }
        public float  DurationSeconds { get; set; }
        public int    SampleRate      { get; set; }
        public int    Channels        { get; set; }
        public int    SampleCount     { get; set; }

        /// <summary>Human-readable generation provenance, e.g. "Synthesized tone (440Hz) 1s @ 44100Hz mono".</summary>
        public string Source { get; set; }
    }
}

namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioClipInfoResult
    {
        public string AssetPath { get; set; }

        // AudioClip runtime data
        public float  Length      { get; set; }
        public int    Frequency   { get; set; }
        public int    Channels    { get; set; }
        public int    Samples     { get; set; }
        public string LoadType    { get; set; }
        public bool   Ambisonic   { get; set; }
        public bool   PreloadAudioData { get; set; }

        // AudioImporter data
        public bool   ForceToMono      { get; set; }
        public bool   LoadInBackground { get; set; }
        public string Platform         { get; set; }
        public bool   HasOverride      { get; set; }
        public string CompressionFormat { get; set; }
        public string SampleRateSetting { get; set; }
        public uint  SampleRateOverride { get; set; }
        public float  Quality           { get; set; }
    }
}

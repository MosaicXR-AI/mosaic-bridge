namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioSetImportSettingsResult
    {
        public string AssetPath         { get; set; }
        public bool   ForceToMono       { get; set; }
        public bool   LoadInBackground  { get; set; }
        public bool   Ambisonic         { get; set; }
        public string Platform          { get; set; }
        public bool   HasOverride       { get; set; }
        public string LoadType          { get; set; }
        public string CompressionFormat { get; set; }
        public string SampleRateSetting { get; set; }
        public uint  SampleRateOverride { get; set; }
        public float  Quality           { get; set; }
        public bool   PreloadAudioData  { get; set; }
    }
}

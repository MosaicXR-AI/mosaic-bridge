namespace Mosaic.Bridge.Tools.Lighting
{
    public sealed class LightingSettingsResult
    {
        public string Action { get; set; }
        public string AssetPath { get; set; }
        public string Lightmapper { get; set; }
        public float LightmapResolution { get; set; }
        public int LightmapMaxSize { get; set; }
        public bool Ao { get; set; }
        public string MixedBakeMode { get; set; }
        public bool BakedGI { get; set; }
        public bool RealtimeGI { get; set; }
        public float IndirectResolution { get; set; }
        public float AoMaxDistance { get; set; }
        public int DirectSampleCount { get; set; }
        public int IndirectSampleCount { get; set; }
        public int MinBounces { get; set; }
        public int MaxBounces { get; set; }
        public string FilteringMode { get; set; }
    }
}

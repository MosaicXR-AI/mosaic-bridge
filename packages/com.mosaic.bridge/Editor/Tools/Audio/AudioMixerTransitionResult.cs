namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioMixerTransitionResult
    {
        public string   MixerName     { get; set; }
        public string[] SnapshotNames { get; set; }
        public float[]  Weights       { get; set; }
        public float    TimeToReach   { get; set; }
        public string   Message       { get; set; }
    }
}

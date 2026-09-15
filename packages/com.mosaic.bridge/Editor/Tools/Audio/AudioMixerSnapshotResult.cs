namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioMixerSnapshotResult
    {
        public string   Operation      { get; set; }
        public string   MixerName      { get; set; }
        public string   SnapshotName   { get; set; }

        /// <summary>"list" only: every snapshot's name.</summary>
        public string[] SnapshotNames  { get; set; }

        public string   Message        { get; set; }
    }
}

namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioCreateMixerResult
    {
        public string AssetPath { get; set; }
        public string MixerName { get; set; }
        public string MasterGroupName { get; set; }

        /// <summary>False when an AudioMixer already existed at AssetPath — this is idempotent,
        /// not a failure.</summary>
        public bool Created { get; set; }

        public string Message { get; set; }
    }
}

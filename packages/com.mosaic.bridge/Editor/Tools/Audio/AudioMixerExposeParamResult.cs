namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioMixerExposeParamResult
    {
        public string   Operation      { get; set; }
        public string   MixerName      { get; set; }

        /// <summary>"list" only: every currently exposed parameter's name.</summary>
        public string[] ExposedParameterNames { get; set; }

        public string   Message        { get; set; }
    }
}

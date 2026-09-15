namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioMixerEffectResult
    {
        public string   Operation   { get; set; }
        public string   MixerName   { get; set; }
        public string   GroupName   { get; set; }
        public int      EffectIndex { get; set; }
        public string   EffectType  { get; set; }
        public float    Value       { get; set; }

        /// <summary>"list": current effect names on the group, in index order.</summary>
        public string[] EffectNames { get; set; }

        /// <summary>"list-types": every valid EffectType name for audio/mixer-effect's "add".</summary>
        public string[] AvailableEffectTypes { get; set; }

        public string Message { get; set; }
    }
}

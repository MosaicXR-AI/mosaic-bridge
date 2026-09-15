namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioSetSourceResult
    {
        public int InstanceId { get; set; }
        public string GameObjectName { get; set; }
        public string ClipName { get; set; }
        public string ResourceName { get; set; }
        public float Volume { get; set; }
        public float Pitch { get; set; }
        public bool Loop { get; set; }
        public bool PlayOnAwake { get; set; }
        public int Priority { get; set; }
        public bool Mute { get; set; }
        public bool BypassEffects { get; set; }
        public bool BypassListenerEffects { get; set; }
        public bool BypassReverbZones { get; set; }
        public float PanStereo { get; set; }
        public float ReverbZoneMix { get; set; }
        public bool Spatialize { get; set; }
        public bool SpatializePostEffects { get; set; }
    }
}

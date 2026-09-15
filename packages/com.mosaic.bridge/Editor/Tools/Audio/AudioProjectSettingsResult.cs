namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioProjectSettingsResult
    {
        public float  Volume                  { get; set; }
        public float  RolloffScale            { get; set; }
        public float  DopplerFactor           { get; set; }
        public string DefaultSpeakerMode      { get; set; }
        public int    SampleRate              { get; set; }
        public int    RequestedDspBufferSize  { get; set; }
        public int    VirtualVoiceCount       { get; set; }
        public int    RealVoiceCount          { get; set; }
        public string SpatializerPlugin       { get; set; }
        public string AmbisonicDecoderPlugin  { get; set; }
        public bool   DisableAudio            { get; set; }
        public bool   EnableOutputSuspension  { get; set; }
        public bool   VirtualizeEffects       { get; set; }

        /// <summary>All spatializer plugin names available on this machine, from AudioSettings.GetSpatializerPluginNames().</summary>
        public string[] AvailableSpatializerPlugins { get; set; }

        public string Message { get; set; }
    }
}

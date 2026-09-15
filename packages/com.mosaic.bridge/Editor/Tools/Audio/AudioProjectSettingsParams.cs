namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioProjectSettingsParams
    {
        /// <summary>Global volume for all sounds, 0..1. Null to leave unchanged.</summary>
        public float? Volume { get; set; }

        /// <summary>Attenuation scale for logarithmic-rolloff sources. Null to leave unchanged.</summary>
        public float? RolloffScale { get; set; }

        /// <summary>How audible the Doppler effect is. Null to leave unchanged.</summary>
        public float? DopplerFactor { get; set; }

        /// <summary>Default speaker mode: Mono, Stereo, Quad, Surround, Mode5point1, Mode7point1,
        /// Prologic, Mode7point1point4. Null to leave unchanged.</summary>
        public string DefaultSpeakerMode { get; set; }

        /// <summary>System output sample rate in Hz. 0 = use the system default. Null to leave unchanged.</summary>
        public int? SampleRate { get; set; }

        /// <summary>Requested DSP buffer size in samples (obsolete API — AudioSettings.SetDSPBufferSize
        /// is the modern replacement and only takes effect after AudioSettings.Reset). 0 = default.
        /// Null to leave unchanged.</summary>
        public int? RequestedDspBufferSize { get; set; }

        /// <summary>Number of virtual voices the audio system manages. Null to leave unchanged.</summary>
        public int? VirtualVoiceCount { get; set; }

        /// <summary>Number of real (audible) voices that can play simultaneously. Null to leave unchanged.</summary>
        public int? RealVoiceCount { get; set; }

        /// <summary>Native spatializer plugin name (validated against AudioSettings.GetSpatializerPluginNames()).
        /// Empty string clears it. Null to leave unchanged.</summary>
        public string SpatializerPlugin { get; set; }

        /// <summary>Native ambisonic decoder plugin name. Empty string clears it. Null to leave unchanged.</summary>
        public string AmbisonicDecoderPlugin { get; set; }

        /// <summary>Deactivates the audio system in standalone builds. Null to leave unchanged.</summary>
        public bool? DisableAudio { get; set; }

        /// <summary>Automatically suspends audio output after silence is detected. Null to leave unchanged.</summary>
        public bool? EnableOutputSuspension { get; set; }

        /// <summary>Dynamically turns off effects/spatializers on culled sources. Null to leave unchanged.</summary>
        public bool? VirtualizeEffects { get; set; }
    }
}

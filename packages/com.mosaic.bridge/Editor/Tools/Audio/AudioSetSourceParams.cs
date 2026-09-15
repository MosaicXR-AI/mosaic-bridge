namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioSetSourceParams
    {
        /// <summary>InstanceId of the GameObject carrying the AudioSource.</summary>
        public int? InstanceId { get; set; }

        /// <summary>Name of the GameObject carrying the AudioSource.</summary>
        public string Name { get; set; }

        /// <summary>Asset path of an AudioClip to assign, e.g. "Assets/Audio/footstep.wav".</summary>
        public string ClipPath { get; set; }

        /// <summary>Asset path of an AudioResource (e.g. an AudioRandomContainer created via
        /// audio/create-random-container) to assign to AudioSource.resource. Takes precedence
        /// over ClipPath when both are set, per AudioSource's own resource/clip relationship.</summary>
        public string ResourcePath { get; set; }

        public float? Volume { get; set; }
        public float? Pitch { get; set; }
        public bool? Loop { get; set; }
        public bool? PlayOnAwake { get; set; }

        /// <summary>0 (highest) .. 256 (lowest). Unity default is 128.</summary>
        public int? Priority { get; set; }
        public bool? Mute { get; set; }
        public bool? BypassEffects { get; set; }
        public bool? BypassListenerEffects { get; set; }
        public bool? BypassReverbZones { get; set; }

        /// <summary>-1 (full left) .. 1 (full right). 2D sources only.</summary>
        public float? PanStereo { get; set; }

        /// <summary>0 .. 1.1. How much of the signal reaches attached reverb zones.</summary>
        public float? ReverbZoneMix { get; set; }

        public bool? Spatialize { get; set; }
        public bool? SpatializePostEffects { get; set; }
    }
}

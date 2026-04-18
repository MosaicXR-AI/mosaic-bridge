namespace Mosaic.Bridge.Tools.Audio
{
    public sealed class AudioCreateSourceParams
    {
        /// <summary>InstanceId of the target GameObject. If null, falls back to Name.</summary>
        public int? InstanceId { get; set; }

        /// <summary>Name of the target GameObject. If both InstanceId and Name are null, a new GO is created.</summary>
        public string Name { get; set; }

        /// <summary>Asset path to an AudioClip (e.g. "Assets/Audio/clip.wav"). Optional.</summary>
        public string ClipPath { get; set; }

        /// <summary>Volume level (0-1). Defaults to 1.</summary>
        public float? Volume { get; set; }

        /// <summary>Pitch multiplier. Defaults to 1.</summary>
        public float? Pitch { get; set; }

        /// <summary>Spatial blend (0 = fully 2D, 1 = fully 3D). Defaults to 0.</summary>
        public float? SpatialBlend { get; set; }

        /// <summary>Whether the AudioSource should loop. Defaults to false.</summary>
        public bool? Loop { get; set; }

        /// <summary>Whether the AudioSource plays automatically on awake. Defaults to true.</summary>
        public bool? PlayOnAwake { get; set; }
    }
}

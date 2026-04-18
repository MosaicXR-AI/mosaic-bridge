#if MOSAIC_HAS_URP
using System.Collections.Generic;
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.URP
{
    public sealed class UrpVolumeParams
    {
        /// <summary>Name of the Volume GameObject.</summary>
        [Required] public string Name { get; set; }

        /// <summary>Whether the volume is global (affects entire scene) or local.</summary>
        public bool IsGlobal { get; set; } = true;

        /// <summary>Volume priority. Higher values take precedence.</summary>
        public float Priority { get; set; } = 0f;

        /// <summary>
        /// Dictionary of override names to enable on the VolumeProfile.
        /// Supported keys: Bloom, ColorAdjustments, Vignette, ChannelMixer,
        /// ChromaticAberration, DepthOfField, FilmGrain, LensDistortion,
        /// MotionBlur, Tonemapping, WhiteBalance.
        /// Values are dictionaries of property name to value.
        /// </summary>
        public Dictionary<string, Dictionary<string, object>> Overrides { get; set; }
    }
}
#endif

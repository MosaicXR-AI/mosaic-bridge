#if MOSAIC_HAS_HDRP
using System.Collections.Generic;
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.HDRP
{
    public sealed class HdrpVolumeParams
    {
        /// <summary>Name of the Volume GameObject.</summary>
        [Required] public string Name { get; set; }

        /// <summary>Whether the volume is global (affects entire scene) or local.</summary>
        public bool IsGlobal { get; set; } = true;

        /// <summary>Volume priority. Higher values take precedence.</summary>
        public float Priority { get; set; } = 0f;

        /// <summary>
        /// Dictionary of HDRP override names to enable on the VolumeProfile.
        /// Supported keys: Exposure, Fog, SSAO, Bloom, ColorAdjustments,
        /// Vignette, DepthOfField, MotionBlur, ChromaticAberration,
        /// Tonemapping, WhiteBalance, ContactShadows, IndirectLightingController.
        /// Values are dictionaries of property name to value (for future property-level config).
        /// </summary>
        public Dictionary<string, Dictionary<string, object>> Overrides { get; set; }
    }
}
#endif

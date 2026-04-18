#if MOSAIC_HAS_HDRP
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.HDRP
{
    public sealed class HdrpLightParams
    {
        /// <summary>Name of the existing GameObject with a Light component to configure.</summary>
        [Required] public string GameObjectName { get; set; }

        /// <summary>Area light shape: "Rectangle", "Disc", or "Tube". Null leaves unchanged.</summary>
        public string AreaLightShape { get; set; }

        /// <summary>Light intensity in the current unit. Null leaves unchanged.</summary>
        public float? Intensity { get; set; }

        /// <summary>Color temperature in Kelvin. Null leaves unchanged.</summary>
        public float? ColorTemperature { get; set; }

        /// <summary>Volumetric dimmer (0-1). Controls how much this light contributes to volumetric fog. Null leaves unchanged.</summary>
        public float? VolumetricDimmer { get; set; }

        /// <summary>Shadow resolution override. Null leaves unchanged.</summary>
        public int? ShadowResolution { get; set; }
    }
}
#endif

namespace Mosaic.Bridge.Tools.AdvancedRendering
{
    /// <summary>
    /// Parameters for the rendering/atmosphere-create tool.
    /// Generates an atmospheric scattering sky (shader + material) using the
    /// Preetham (1999), Bruneton (2008) or O'Neil (GPU Gems 2) models.
    /// </summary>
    public sealed class RenderingAtmosphereCreateParams
    {
        /// <summary>
        /// Scattering model: "preetham", "bruneton", or "oneil". Default: "preetham".
        /// </summary>
        public string   Model                 { get; set; }

        /// <summary>
        /// Direction TOWARD the sun (will be normalized). Default: [0, 0.5, 1].
        /// </summary>
        public float[]  SunDirection          { get; set; }

        /// <summary>
        /// Rayleigh scattering coefficients (RGB) in units of 10^-6 m^-1.
        /// Default: [5.8, 13.5, 33.1].
        /// </summary>
        public float[]  RayleighCoefficients  { get; set; }

        /// <summary>
        /// Mie scattering coefficient in units of 10^-6 m^-1. Default: 21.0.
        /// </summary>
        public float?   MieCoefficient        { get; set; }

        /// <summary>
        /// Planet radius in kilometers. Default: 6360.
        /// </summary>
        public float?   PlanetRadius          { get; set; }

        /// <summary>
        /// Atmosphere thickness in kilometers. Default: 80.
        /// </summary>
        public float?   AtmosphereHeight      { get; set; }

        /// <summary>
        /// Sun intensity multiplier. Default: 20.0.
        /// </summary>
        public float?   SunIntensity          { get; set; }

        /// <summary>
        /// Turbidity (Preetham only). Default: 2.0.
        /// </summary>
        public float?   Turbidity             { get; set; }

        /// <summary>
        /// Output type: "skybox_material" (shader + mat), "shader" (shader only),
        /// or "compute_lut" (shader + LUT-generating compute shader). Default: "skybox_material".
        /// </summary>
        public string   OutputType            { get; set; }

        /// <summary>
        /// Optional base name for generated assets. Defaults to a model-based name.
        /// </summary>
        public string   OutputName            { get; set; }

        /// <summary>
        /// Save directory under Assets/. Default: "Assets/Generated/Rendering/".
        /// </summary>
        public string   SavePath              { get; set; }

        /// <summary>
        /// If true, assigns the generated material as RenderSettings.skybox.
        /// Default: false.
        /// </summary>
        public bool?    ApplyToScene          { get; set; }
    }
}

namespace Mosaic.Bridge.Tools.AdvancedRendering
{
    /// <summary>
    /// Parameters for configuring a URP Screen-Space Reflections (SSR) render feature.
    /// </summary>
    public sealed class ShaderSsrConfigureParams
    {
        /// <summary>Quality preset: "low", "medium", "high", "ultra". Adjusts MaxSteps/StepSize.</summary>
        public string  Quality      { get; set; }
        public int?    MaxSteps     { get; set; }
        public float?  StepSize     { get; set; }
        /// <summary>Ray-hit thickness tolerance (world-space depth delta that counts as a hit).</summary>
        public float?  Thickness    { get; set; }
        /// <summary>Schlick fresnel attenuation factor.</summary>
        public float?  FresnelFade  { get; set; }
        public float?  MaxDistance  { get; set; }
        /// <summary>GameObject name of the target camera. Defaults to Camera.main.</summary>
        public string  TargetCamera { get; set; }
        public string  OutputName   { get; set; }
        /// <summary>Asset-relative save path, default "Assets/Generated/Rendering/".</summary>
        public string  SavePath     { get; set; }
    }
}

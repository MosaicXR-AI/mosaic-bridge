namespace Mosaic.Bridge.Tools.AdvancedRendering
{
    /// <summary>
    /// Result envelope for the rendering/atmosphere-create tool.
    /// </summary>
    public sealed class RenderingAtmosphereCreateResult
    {
        /// <summary>Primary asset path (material for skybox_material, shader otherwise).</summary>
        public string AssetPath       { get; set; }

        /// <summary>Scattering model used ("preetham", "bruneton", or "oneil").</summary>
        public string Model           { get; set; }

        /// <summary>Output type used ("skybox_material", "shader", or "compute_lut").</summary>
        public string OutputType      { get; set; }

        /// <summary>True if the material was assigned as RenderSettings.skybox.</summary>
        public bool   AppliedToScene  { get; set; }

        /// <summary>Path to the generated .shader file.</summary>
        public string ShaderPath      { get; set; }

        /// <summary>Path to the generated .mat file (null if OutputType != skybox_material).</summary>
        public string MaterialPath    { get; set; }

        /// <summary>Path to the generated .compute LUT generator (null unless OutputType == compute_lut).</summary>
        public string ComputeLutPath  { get; set; }
    }
}

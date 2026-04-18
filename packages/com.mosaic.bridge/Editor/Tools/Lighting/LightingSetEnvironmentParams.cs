namespace Mosaic.Bridge.Tools.Lighting
{
    public sealed class LightingSetEnvironmentParams
    {
        public string AmbientMode { get; set; }       // Skybox, Trilight, Flat, Custom
        public float[] AmbientColor { get; set; }     // [r,g,b] 0-1 range
        public float? AmbientIntensity { get; set; }
        public string SkyboxMaterial { get; set; }    // asset path to skybox material
        public bool? FogEnabled { get; set; }
        public float[] FogColor { get; set; }         // [r,g,b] 0-1 range
        public float? FogDensity { get; set; }
        public string FogMode { get; set; }           // Linear, Exponential, ExponentialSquared
    }
}

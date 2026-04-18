namespace Mosaic.Bridge.Tools.Lighting
{
    public sealed class LightingSetEnvironmentResult
    {
        public string AmbientMode { get; set; }
        public float[] AmbientColor { get; set; }
        public float AmbientIntensity { get; set; }
        public bool FogEnabled { get; set; }
        public float[] FogColor { get; set; }
        public float FogDensity { get; set; }
        public int PropertiesChanged { get; set; }
    }
}

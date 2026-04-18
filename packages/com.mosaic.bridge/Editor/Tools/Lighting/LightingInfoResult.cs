namespace Mosaic.Bridge.Tools.Lighting
{
    public sealed class LightingInfoResult
    {
        public LightInfo[] Lights { get; set; }
        public EnvironmentInfo Environment { get; set; }
    }

    public sealed class LightInfo
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string HierarchyPath { get; set; }
        public string LightType { get; set; }
        public float[] Color { get; set; }
        public float Intensity { get; set; }
        public float Range { get; set; }
        public float SpotAngle { get; set; }
        public string Shadows { get; set; }
        public float ColorTemperature { get; set; }
        public float BounceIntensity { get; set; }
        public bool Enabled { get; set; }
    }

    public sealed class EnvironmentInfo
    {
        public string AmbientMode { get; set; }
        public float[] AmbientColor { get; set; }
        public float AmbientIntensity { get; set; }
        public string SkyboxMaterial { get; set; }
        public bool FogEnabled { get; set; }
        public float[] FogColor { get; set; }
        public float FogDensity { get; set; }
        public string FogMode { get; set; }
    }
}

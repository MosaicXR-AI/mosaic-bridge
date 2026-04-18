namespace Mosaic.Bridge.Tools.Lighting
{
    public sealed class LightingSetPropertiesParams
    {
        public int InstanceId { get; set; }       // 0 means not specified
        public string Name { get; set; }          // find light by GameObject name
        public float[] Color { get; set; }        // [r,g,b] or [r,g,b,a] 0-1 range
        public float? Intensity { get; set; }
        public float? Range { get; set; }
        public float? SpotAngle { get; set; }
        public string Shadows { get; set; }       // None, Hard, Soft
        public float? ColorTemperature { get; set; }
        public float? BounceIntensity { get; set; }
    }
}

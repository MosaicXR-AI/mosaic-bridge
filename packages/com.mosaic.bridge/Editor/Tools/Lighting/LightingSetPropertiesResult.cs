namespace Mosaic.Bridge.Tools.Lighting
{
    public sealed class LightingSetPropertiesResult
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string LightType { get; set; }
        public float Intensity { get; set; }
        public string Shadows { get; set; }
        public int PropertiesChanged { get; set; }
    }
}

namespace Mosaic.Bridge.Tools.Lighting
{
    public sealed class LightingCreateResult
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string HierarchyPath { get; set; }
        public string LightType { get; set; }
        public float Intensity { get; set; }
    }
}

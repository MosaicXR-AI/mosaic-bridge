namespace Mosaic.Bridge.Tools.Lighting
{
    public sealed class LightingInfoParams
    {
        public int InstanceId { get; set; }  // 0 = query all lights
        public string Name { get; set; }     // optional: find specific light by name
    }
}

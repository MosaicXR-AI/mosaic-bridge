#if MOSAIC_HAS_HDRP
namespace Mosaic.Bridge.Tools.HDRP
{
    public sealed class HdrpLightResult
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string HierarchyPath { get; set; }
        public string LightType { get; set; }
        public string AreaLightShape { get; set; }
        public float Intensity { get; set; }
        public float ColorTemperature { get; set; }
        public float VolumetricDimmer { get; set; }
        public int ShadowResolution { get; set; }
    }
}
#endif

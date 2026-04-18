#if MOSAIC_HAS_URP
namespace Mosaic.Bridge.Tools.URP
{
    public sealed class UrpRendererFeatureResult
    {
        public string Action { get; set; }
        public string FeatureName { get; set; }
        public string FeatureType { get; set; }
        public UrpRendererFeatureInfo[] Features { get; set; }
        public string RendererName { get; set; }
    }

    public sealed class UrpRendererFeatureInfo
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public bool IsActive { get; set; }
    }
}
#endif

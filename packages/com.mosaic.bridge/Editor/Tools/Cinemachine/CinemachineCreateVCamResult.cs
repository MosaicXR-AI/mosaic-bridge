#if MOSAIC_HAS_CINEMACHINE
namespace Mosaic.Bridge.Tools.Cinemachine
{
    public sealed class CinemachineCreateVCamResult
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public string HierarchyPath { get; set; }
        public string BodyType { get; set; }
        public string AimType { get; set; }
        public string NoiseType { get; set; }
        public int Priority { get; set; }
        public float Dutch { get; set; }
        public float OrthographicSize { get; set; }
        public string LensModeOverride { get; set; }
        public bool InputControllerAdded { get; set; }
        public string[] DiscoveredControllerNames { get; set; }
    }
}
#endif

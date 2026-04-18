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
        public int Priority { get; set; }
    }
}
#endif

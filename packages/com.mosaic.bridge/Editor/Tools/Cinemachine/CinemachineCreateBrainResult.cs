#if MOSAIC_HAS_CINEMACHINE
namespace Mosaic.Bridge.Tools.Cinemachine
{
    public sealed class CinemachineCreateBrainResult
    {
        public int InstanceId { get; set; }
        public string CameraName { get; set; }
        public float DefaultBlend { get; set; }
        public string BlendType { get; set; }
        public bool AlreadyExisted { get; set; }
    }
}
#endif

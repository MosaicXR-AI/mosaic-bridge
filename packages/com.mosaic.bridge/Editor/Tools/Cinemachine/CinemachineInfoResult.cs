#if MOSAIC_HAS_CINEMACHINE
namespace Mosaic.Bridge.Tools.Cinemachine
{
    public sealed class CinemachineInfoResult
    {
        public CinemachineVCamInfo[] VirtualCameras { get; set; }
        public CinemachineBrainInfo[] Brains { get; set; }
        public string ActiveCamera { get; set; }
    }

    public sealed class CinemachineVCamInfo
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        public int Priority { get; set; }
        public string FollowTarget { get; set; }
        public string LookAtTarget { get; set; }
        public bool IsLive { get; set; }
        public string HierarchyPath { get; set; }
        public string[] BodyComponents { get; set; }
        public string[] AimComponents { get; set; }
    }

    public sealed class CinemachineBrainInfo
    {
        public int InstanceId { get; set; }
        public string CameraName { get; set; }
        public float DefaultBlendTime { get; set; }
        public string DefaultBlendStyle { get; set; }
    }
}
#endif

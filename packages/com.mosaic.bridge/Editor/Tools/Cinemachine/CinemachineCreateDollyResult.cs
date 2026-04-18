#if MOSAIC_HAS_CINEMACHINE
namespace Mosaic.Bridge.Tools.Cinemachine
{
    public sealed class CinemachineCreateDollyResult
    {
        public int TrackInstanceId { get; set; }
        public string TrackName { get; set; }
        public int WaypointCount { get; set; }
        public bool AutoDollyEnabled { get; set; }
        public string AttachedToVCam { get; set; }
    }
}
#endif

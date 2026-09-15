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
        public float CameraPosition { get; set; }
        public string PositionUnits { get; set; }
        public string CameraRotation { get; set; }
        public string AutoDollyMethod { get; set; }
        public string CartName { get; set; }
        public int CartInstanceId { get; set; }
    }
}
#endif

#if MOSAIC_HAS_CINEMACHINE && MOSAIC_HAS_TIMELINE
namespace Mosaic.Bridge.Tools.Cinemachine
{
    public sealed class CinemachineTimelineShotResult
    {
        public int TrackIndex { get; set; }
        public string VCamName { get; set; }
        public double Start { get; set; }
        public double Duration { get; set; }
        public bool BrainFoundInScene { get; set; }
    }
}
#endif

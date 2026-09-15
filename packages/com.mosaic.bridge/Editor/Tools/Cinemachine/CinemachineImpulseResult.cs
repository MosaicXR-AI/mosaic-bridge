#if MOSAIC_HAS_CINEMACHINE
namespace Mosaic.Bridge.Tools.Cinemachine
{
    public sealed class CinemachineImpulseResult
    {
        public string Action { get; set; }
        public string TargetName { get; set; }
        public string ImpulseShape { get; set; }
        public float ImpulseDuration { get; set; }
        public int ImpulseChannel { get; set; }
    }
}
#endif

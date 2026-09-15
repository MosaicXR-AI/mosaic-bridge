#if MOSAIC_HAS_TIMELINE
namespace Mosaic.Bridge.Tools.Timeline
{
    public sealed class TimelineSetDirectorResult
    {
        public int InstanceId { get; set; }
        public string GameObjectName { get; set; }
        public string TimelineAssetPath { get; set; }
        public bool PlayOnAwake { get; set; }
        public string WrapMode { get; set; }
        public string UpdateMode { get; set; }
        public double InitialTime { get; set; }
    }
}
#endif

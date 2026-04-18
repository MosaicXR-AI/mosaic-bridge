#if MOSAIC_HAS_TIMELINE
namespace Mosaic.Bridge.Tools.Timeline
{
    public sealed class TimelineSetDirectorResult
    {
        public int InstanceId { get; set; }
        public string GameObjectName { get; set; }
        public string TimelineAssetPath { get; set; }
    }
}
#endif

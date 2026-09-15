#if MOSAIC_HAS_TIMELINE
namespace Mosaic.Bridge.Tools.Timeline
{
    public sealed class TimelineSignalResult
    {
        public string Action { get; set; }

        // -- create-asset --
        public string AssetPath { get; set; }

        // -- add-emitter --
        public int TrackIndex { get; set; }
        public double Time { get; set; }
        public bool Retroactive { get; set; }
        public bool EmitOnce { get; set; }

        // -- add-receiver --
        public string ReceiverName { get; set; }
        public int ListenerIndex { get; set; }
        public string CallState { get; set; }
    }
}
#endif

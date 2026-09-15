#if MOSAIC_HAS_TIMELINE
namespace Mosaic.Bridge.Tools.Timeline
{
    public sealed class TimelineBindResult
    {
        public string Action { get; set; }
        public int DirectorInstanceId { get; set; }
        public int TrackIndex { get; set; }
        public string TrackName { get; set; }

        // -- bind --
        public int TargetInstanceId { get; set; }
        public string TargetName { get; set; }
        /// <summary>Set when the target was a GameObject and the track's TrackBindingTypeAttribute
        /// named a Component type — the actual bound object is that component, found via GetComponent.</summary>
        public string BoundComponentType { get; set; }

        // -- set-reference --
        public int ClipIndex { get; set; }
        public string ReferenceField { get; set; }
        public string ReferenceTargetName { get; set; }
    }
}
#endif

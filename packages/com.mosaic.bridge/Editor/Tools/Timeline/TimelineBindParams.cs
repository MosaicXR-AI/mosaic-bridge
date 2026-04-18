#if MOSAIC_HAS_TIMELINE
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Timeline
{
    public sealed class TimelineBindParams
    {
        [Required] public int DirectorInstanceId { get; set; }
        [Required] public int TrackIndex { get; set; }
        [Required] public int TargetInstanceId { get; set; }
    }
}
#endif

#if MOSAIC_HAS_TIMELINE
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Timeline
{
    public sealed class TimelineCreateParams
    {
        [Required] public string Name { get; set; }
        [Required] public string Path { get; set; }
    }
}
#endif

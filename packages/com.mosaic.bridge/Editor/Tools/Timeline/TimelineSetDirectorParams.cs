#if MOSAIC_HAS_TIMELINE
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Timeline
{
    public sealed class TimelineSetDirectorParams
    {
        public int InstanceId { get; set; }
        public string Name { get; set; }
        [Required] public string TimelineAssetPath { get; set; }
    }
}
#endif

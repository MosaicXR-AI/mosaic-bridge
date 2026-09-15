#if MOSAIC_HAS_CINEMACHINE && MOSAIC_HAS_TIMELINE
using Mosaic.Bridge.Contracts.Attributes;

namespace Mosaic.Bridge.Tools.Cinemachine
{
    public sealed class CinemachineTimelineShotParams
    {
        [Required] public string TimelineAssetPath { get; set; }
        /// <summary>Index into the timeline's tracks (must be a CinemachineTrack, from
        /// timeline/add-track TrackType=Cinemachine).</summary>
        [Required] public int TrackIndex { get; set; }

        /// <summary>Name of the PlayableDirector's GameObject — needed to resolve the shot's
        /// ExposedReference via SetReferenceValue.</summary>
        [Required] public int DirectorInstanceId { get; set; }
        public string DirectorPath { get; set; }

        /// <summary>Name of the vcam GameObject this shot plays.</summary>
        [Required] public string VCamName { get; set; }

        public double Start { get; set; }
        public double Duration { get; set; } = 2.0;
    }
}
#endif

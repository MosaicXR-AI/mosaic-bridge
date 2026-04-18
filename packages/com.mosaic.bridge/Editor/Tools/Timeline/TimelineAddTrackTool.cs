#if MOSAIC_HAS_TIMELINE
using System;
using System.Linq;
using UnityEditor;
using UnityEngine.Timeline;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Timeline
{
    public static class TimelineAddTrackTool
    {
        [MosaicTool("timeline/add-track",
                    "Adds a track to a TimelineAsset",
                    isReadOnly: false)]
        public static ToolResult<TimelineAddTrackResult> AddTrack(TimelineAddTrackParams p)
        {
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(p.AssetPath);
            if (timeline == null)
                return ToolResult<TimelineAddTrackResult>.Fail(
                    $"TimelineAsset not found at '{p.AssetPath}'", ErrorCodes.NOT_FOUND);

            TrackAsset track;
            switch (p.TrackType?.ToLowerInvariant())
            {
                case "animation":
                    track = timeline.CreateTrack<AnimationTrack>(null, p.Name);
                    break;
                case "audio":
                    track = timeline.CreateTrack<AudioTrack>(null, p.Name);
                    break;
                case "activation":
                    track = timeline.CreateTrack<ActivationTrack>(null, p.Name);
                    break;
                case "signal":
                    track = timeline.CreateTrack<SignalTrack>(null, p.Name);
                    break;
                case "control":
                    track = timeline.CreateTrack<ControlTrack>(null, p.Name);
                    break;
                default:
                    return ToolResult<TimelineAddTrackResult>.Fail(
                        $"Unknown track type '{p.TrackType}'. Valid types: Animation, Audio, Activation, Signal, Control",
                        ErrorCodes.INVALID_PARAM);
            }

            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();

            int trackIndex = timeline.GetOutputTracks().ToList().IndexOf(track);

            return ToolResult<TimelineAddTrackResult>.Ok(new TimelineAddTrackResult
            {
                TrackIndex = trackIndex,
                TrackType = p.TrackType,
                Name = track.name
            });
        }
    }
}
#endif

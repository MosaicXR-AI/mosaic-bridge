#if MOSAIC_HAS_TIMELINE
using System.Linq;
using UnityEditor;
using UnityEngine.Timeline;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Timeline
{
    public static class TimelineRemoveTool
    {
        [MosaicTool("timeline/remove",
                    "Removes a track, clip, or marker from a TimelineAsset. Target=track deletes " +
                    "TrackIndex (and its clips/subtracks). Target=clip deletes ClipIndex on TrackIndex. " +
                    "Target=marker deletes MarkerIndex on TrackIndex, or on the timeline's own global " +
                    "marker track when TrackIndex is omitted.",
                    isReadOnly: false)]
        public static ToolResult<TimelineRemoveResult> Remove(TimelineRemoveParams p)
        {
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(p.AssetPath);
            if (timeline == null)
                return ToolResult<TimelineRemoveResult>.Fail(
                    $"TimelineAsset not found at '{p.AssetPath}'", ErrorCodes.NOT_FOUND);

            ToolResult<TimelineRemoveResult> result;
            switch (p.Target?.ToLowerInvariant())
            {
                case "track":  result = RemoveTrack(p, timeline); break;
                case "clip":   result = RemoveClip(p, timeline); break;
                case "marker": result = RemoveMarker(p, timeline); break;
                default:
                    return ToolResult<TimelineRemoveResult>.Fail(
                        $"Unknown Target '{p.Target}'. Valid: track, clip, marker", ErrorCodes.INVALID_PARAM);
            }

            if (result.Success)
            {
                EditorUtility.SetDirty(timeline);
                AssetDatabase.SaveAssets();
            }
            return result;
        }

        private static ToolResult<TimelineRemoveResult> RemoveTrack(TimelineRemoveParams p, TimelineAsset timeline)
        {
            if (!p.TrackIndex.HasValue)
                return ToolResult<TimelineRemoveResult>.Fail("TrackIndex is required for Target=track", ErrorCodes.INVALID_PARAM);

            var tracks = timeline.GetOutputTracks().ToList();
            if (p.TrackIndex.Value < 0 || p.TrackIndex.Value >= tracks.Count)
                return ToolResult<TimelineRemoveResult>.Fail(
                    $"TrackIndex {p.TrackIndex.Value} is out of range (0..{tracks.Count - 1})", ErrorCodes.OUT_OF_RANGE);

            var track = tracks[p.TrackIndex.Value];
            var name = track.name;
            if (!timeline.DeleteTrack(track))
                return ToolResult<TimelineRemoveResult>.Fail($"Failed to delete track '{name}'", ErrorCodes.INTERNAL_ERROR);

            return ToolResult<TimelineRemoveResult>.Ok(new TimelineRemoveResult { Target = "track", RemovedName = name });
        }

        private static ToolResult<TimelineRemoveResult> RemoveClip(TimelineRemoveParams p, TimelineAsset timeline)
        {
            if (!p.TrackIndex.HasValue || !p.ClipIndex.HasValue)
                return ToolResult<TimelineRemoveResult>.Fail(
                    "TrackIndex and ClipIndex are required for Target=clip", ErrorCodes.INVALID_PARAM);

            var tracks = timeline.GetOutputTracks().ToList();
            if (p.TrackIndex.Value < 0 || p.TrackIndex.Value >= tracks.Count)
                return ToolResult<TimelineRemoveResult>.Fail(
                    $"TrackIndex {p.TrackIndex.Value} is out of range (0..{tracks.Count - 1})", ErrorCodes.OUT_OF_RANGE);

            var clips = tracks[p.TrackIndex.Value].GetClips().ToList();
            if (p.ClipIndex.Value < 0 || p.ClipIndex.Value >= clips.Count)
                return ToolResult<TimelineRemoveResult>.Fail(
                    $"ClipIndex {p.ClipIndex.Value} is out of range (0..{clips.Count - 1})", ErrorCodes.OUT_OF_RANGE);

            var clip = clips[p.ClipIndex.Value];
            var name = clip.displayName;
            if (!timeline.DeleteClip(clip))
                return ToolResult<TimelineRemoveResult>.Fail($"Failed to delete clip '{name}'", ErrorCodes.INTERNAL_ERROR);

            return ToolResult<TimelineRemoveResult>.Ok(new TimelineRemoveResult { Target = "clip", RemovedName = name });
        }

        private static ToolResult<TimelineRemoveResult> RemoveMarker(TimelineRemoveParams p, TimelineAsset timeline)
        {
            if (!p.MarkerIndex.HasValue)
                return ToolResult<TimelineRemoveResult>.Fail("MarkerIndex is required for Target=marker", ErrorCodes.INVALID_PARAM);

            TrackAsset host;
            if (p.TrackIndex.HasValue)
            {
                var tracks = timeline.GetOutputTracks().ToList();
                if (p.TrackIndex.Value < 0 || p.TrackIndex.Value >= tracks.Count)
                    return ToolResult<TimelineRemoveResult>.Fail(
                        $"TrackIndex {p.TrackIndex.Value} is out of range (0..{tracks.Count - 1})", ErrorCodes.OUT_OF_RANGE);
                host = tracks[p.TrackIndex.Value];
            }
            else
            {
                if (timeline.markerTrack == null)
                    return ToolResult<TimelineRemoveResult>.Fail(
                        "TimelineAsset has no global marker track", ErrorCodes.NOT_FOUND);
                host = timeline.markerTrack;
            }

            var markers = host.GetMarkers().ToList();
            if (p.MarkerIndex.Value < 0 || p.MarkerIndex.Value >= markers.Count)
                return ToolResult<TimelineRemoveResult>.Fail(
                    $"MarkerIndex {p.MarkerIndex.Value} is out of range (0..{markers.Count - 1})", ErrorCodes.OUT_OF_RANGE);

            var marker = markers[p.MarkerIndex.Value];
            var name = $"{marker.GetType().Name}@{marker.time:0.###}";
            if (!host.DeleteMarker(marker))
                return ToolResult<TimelineRemoveResult>.Fail($"Failed to delete marker '{name}'", ErrorCodes.INTERNAL_ERROR);

            return ToolResult<TimelineRemoveResult>.Ok(new TimelineRemoveResult { Target = "marker", RemovedName = name });
        }
    }
}
#endif

#if MOSAIC_HAS_TIMELINE
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Timeline
{
    public static class TimelineAddClipTool
    {
        [MosaicTool("timeline/add-clip",
                    "Adds a clip to a track in a TimelineAsset",
                    isReadOnly: false)]
        public static ToolResult<TimelineAddClipResult> AddClip(TimelineAddClipParams p)
        {
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(p.AssetPath);
            if (timeline == null)
                return ToolResult<TimelineAddClipResult>.Fail(
                    $"TimelineAsset not found at '{p.AssetPath}'", ErrorCodes.NOT_FOUND);

            var tracks = timeline.GetOutputTracks().ToList();
            if (p.TrackIndex < 0 || p.TrackIndex >= tracks.Count)
                return ToolResult<TimelineAddClipResult>.Fail(
                    $"TrackIndex {p.TrackIndex} is out of range (0..{tracks.Count - 1})",
                    ErrorCodes.OUT_OF_RANGE);

            var track = tracks[p.TrackIndex];

            TimelineClip clip;

            // If a clip asset path is provided, load it and create a clip from it
            if (!string.IsNullOrEmpty(p.ClipAssetPath))
            {
                var clipAsset = AssetDatabase.LoadAssetAtPath<Object>(p.ClipAssetPath);
                if (clipAsset == null)
                    return ToolResult<TimelineAddClipResult>.Fail(
                        $"Clip asset not found at '{p.ClipAssetPath}'", ErrorCodes.NOT_FOUND);

                // For animation clips on animation tracks
                if (track is AnimationTrack animTrack && clipAsset is AnimationClip animClip)
                {
                    clip = animTrack.CreateClip(animClip);
                }
                else
                {
                    clip = track.CreateDefaultClip();
                }
            }
            else
            {
                clip = track.CreateDefaultClip();
            }

            clip.start = p.Start;
            clip.duration = p.Duration > 0 ? p.Duration : 1.0;

            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();

            return ToolResult<TimelineAddClipResult>.Ok(new TimelineAddClipResult
            {
                TrackIndex = p.TrackIndex,
                ClipName = clip.displayName,
                Start = clip.start,
                Duration = clip.duration
            });
        }
    }
}
#endif

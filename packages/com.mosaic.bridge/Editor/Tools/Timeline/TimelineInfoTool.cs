#if MOSAIC_HAS_TIMELINE
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Timeline
{
    public static class TimelineInfoTool
    {
        [MosaicTool("timeline/info",
                    "Queries timeline information including tracks, clips, bindings, and duration",
                    isReadOnly: true)]
        public static ToolResult<TimelineInfoResult> Info(TimelineInfoParams p)
        {
            TimelineAsset timeline = null;
            PlayableDirector director = null;
            string assetPath = p.AssetPath;

            // Try to load from asset path first
            if (!string.IsNullOrEmpty(p.AssetPath))
            {
                timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(p.AssetPath);
            }

            // Try to get from director instance
            if (timeline == null && p.DirectorInstanceId != 0)
            {
                var go = UnityEngine.Resources.EntityIdToObject(p.DirectorInstanceId) as GameObject;
                if (go != null)
                {
                    director = go.GetComponent<PlayableDirector>();
                    if (director != null)
                    {
                        timeline = director.playableAsset as TimelineAsset;
                        if (timeline != null)
                            assetPath = AssetDatabase.GetAssetPath(timeline);
                    }
                }
            }

            if (timeline == null)
                return ToolResult<TimelineInfoResult>.Fail(
                    "TimelineAsset not found. Provide a valid AssetPath or DirectorInstanceId.",
                    ErrorCodes.NOT_FOUND);

            var outputTracks = timeline.GetOutputTracks().ToList();
            var trackInfos = new List<TimelineTrackInfo>();

            for (int i = 0; i < outputTracks.Count; i++)
            {
                var track = outputTracks[i];
                var clips = new List<TimelineClipInfo>();

                foreach (var clip in track.GetClips())
                {
                    clips.Add(new TimelineClipInfo
                    {
                        DisplayName = clip.displayName,
                        Start = clip.start,
                        Duration = clip.duration,
                        End = clip.end
                    });
                }

                // Get binding info if we have a director
                string boundObjectName = null;
                if (director != null)
                {
                    var bound = director.GetGenericBinding(track);
                    if (bound != null)
                        boundObjectName = bound is GameObject go ? go.name : bound.name;
                }

                trackInfos.Add(new TimelineTrackInfo
                {
                    Index = i,
                    Name = track.name,
                    Type = track.GetType().Name.Replace("Track", ""),
                    Muted = track.muted,
                    ClipCount = clips.Count,
                    Clips = clips,
                    BoundObjectName = boundObjectName
                });
            }

            return ToolResult<TimelineInfoResult>.Ok(new TimelineInfoResult
            {
                AssetPath = assetPath,
                Name = timeline.name,
                Duration = timeline.duration,
                FrameRate = timeline.editorSettings.frameRate,
                TrackCount = outputTracks.Count,
                Tracks = trackInfos
            });
        }
    }
}
#endif

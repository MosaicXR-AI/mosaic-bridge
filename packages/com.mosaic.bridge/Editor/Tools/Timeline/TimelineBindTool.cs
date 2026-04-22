#if MOSAIC_HAS_TIMELINE
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
    public static class TimelineBindTool
    {
        [MosaicTool("timeline/bind",
                    "Binds a track in a PlayableDirector to a target object",
                    isReadOnly: false)]
        public static ToolResult<TimelineBindResult> Bind(TimelineBindParams p)
        {
            // Resolve the director
            var directorObj = UnityEngine.Resources.EntityIdToObject(p.DirectorInstanceId) as GameObject;
            if (directorObj == null)
                return ToolResult<TimelineBindResult>.Fail(
                    $"GameObject with InstanceId {p.DirectorInstanceId} not found",
                    ErrorCodes.NOT_FOUND);

            var director = directorObj.GetComponent<PlayableDirector>();
            if (director == null)
                return ToolResult<TimelineBindResult>.Fail(
                    $"No PlayableDirector component on '{directorObj.name}'",
                    ErrorCodes.NOT_FOUND);

            var timeline = director.playableAsset as TimelineAsset;
            if (timeline == null)
                return ToolResult<TimelineBindResult>.Fail(
                    "PlayableDirector has no TimelineAsset assigned",
                    ErrorCodes.NOT_FOUND);

            // Resolve the track
            var tracks = timeline.GetOutputTracks().ToList();
            if (p.TrackIndex < 0 || p.TrackIndex >= tracks.Count)
                return ToolResult<TimelineBindResult>.Fail(
                    $"TrackIndex {p.TrackIndex} is out of range (0..{tracks.Count - 1})",
                    ErrorCodes.OUT_OF_RANGE);

            var track = tracks[p.TrackIndex];

            // Resolve the target
            var target = UnityEngine.Resources.EntityIdToObject(p.TargetInstanceId);
            if (target == null)
                return ToolResult<TimelineBindResult>.Fail(
                    $"Target object with InstanceId {p.TargetInstanceId} not found",
                    ErrorCodes.NOT_FOUND);

            Undo.RecordObject(director, "Mosaic: Bind Timeline Track");
            director.SetGenericBinding(track, target);
            EditorUtility.SetDirty(director);

            string targetName = target is GameObject go ? go.name : target.name;

            return ToolResult<TimelineBindResult>.Ok(new TimelineBindResult
            {
                DirectorInstanceId = p.DirectorInstanceId,
                TrackIndex = p.TrackIndex,
                TrackName = track.name,
                TargetInstanceId = p.TargetInstanceId,
                TargetName = targetName
            });
        }
    }
}
#endif

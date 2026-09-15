#if MOSAIC_HAS_TIMELINE
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;
using Mosaic.Bridge.Core.Scenes;

namespace Mosaic.Bridge.Tools.Timeline
{
    public static class TimelineBindTool
    {
        [MosaicTool("timeline/bind",
                    "bind (default): binds a track in a PlayableDirector to a target — by " +
                    "InstanceId or by name/path (O4 §3.3 GameObjectResolver). When the track's own " +
                    "TrackBindingTypeAttribute names a Component type and the target resolves to a " +
                    "GameObject, the matching component is bound automatically (e.g. an AnimationTrack " +
                    "auto-picks the target's Animator). set-reference: sets an ExposedReference field " +
                    "on a clip's PlayableAsset — currently ControlPlayableAsset.sourceGameObject " +
                    "(ClipIndex + ReferenceInstanceId/ReferencePath) — the #1 reported cause of an " +
                    "unbound/inert timeline.",
                    isReadOnly: false)]
        public static ToolResult<TimelineBindResult> Bind(TimelineBindParams p)
        {
            if (!GameObjectResolver.TryResolve(p.DirectorInstanceId, p.DirectorPath, out var directorObj, out var directorError))
                return ToolResult<TimelineBindResult>.Fail(directorError, ErrorCodes.NOT_FOUND);

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

            var tracks = timeline.GetOutputTracks().ToList();
            if (p.TrackIndex < 0 || p.TrackIndex >= tracks.Count)
                return ToolResult<TimelineBindResult>.Fail(
                    $"TrackIndex {p.TrackIndex} is out of range (0..{tracks.Count - 1})",
                    ErrorCodes.OUT_OF_RANGE);

            var track = tracks[p.TrackIndex];

            switch (p.Action?.ToLowerInvariant())
            {
                case "set-reference":
                    return SetReference(p, director, track);
                case "bind":
                case null:
                case "":
                    return DoBind(p, director, track);
                default:
                    return ToolResult<TimelineBindResult>.Fail(
                        $"Unknown Action '{p.Action}'. Valid: bind, set-reference", ErrorCodes.INVALID_PARAM);
            }
        }

        private static ToolResult<TimelineBindResult> DoBind(TimelineBindParams p, PlayableDirector director, TrackAsset track)
        {
            if (!GameObjectResolver.TryResolve(p.TargetInstanceId, p.TargetPath, out var targetGo, out var targetError))
                return ToolResult<TimelineBindResult>.Fail(targetError, ErrorCodes.NOT_FOUND);

            Object target = targetGo;
            string boundComponentType = null;

            var bindingAttr = track.GetType().GetCustomAttributes(typeof(TrackBindingTypeAttribute), true)
                .Cast<TrackBindingTypeAttribute>().FirstOrDefault();
            if (bindingAttr != null && bindingAttr.type != null && !typeof(GameObject).IsAssignableFrom(bindingAttr.type))
            {
                var component = targetGo.GetComponent(bindingAttr.type);
                if (component == null)
                    return ToolResult<TimelineBindResult>.Fail(
                        $"'{targetGo.name}' has no {bindingAttr.type.Name} component — required by " +
                        $"{track.GetType().Name}'s binding type.", ErrorCodes.NOT_FOUND);
                target = component;
                boundComponentType = bindingAttr.type.Name;
            }

            Undo.RecordObject(director, "Mosaic: Bind Timeline Track");
            director.SetGenericBinding(track, target);
            EditorUtility.SetDirty(director);

            return ToolResult<TimelineBindResult>.Ok(new TimelineBindResult
            {
                Action = "bind",
                DirectorInstanceId = director.gameObject.GetInstanceID(),
                TrackIndex = p.TrackIndex,
                TrackName = track.name,
                TargetInstanceId = targetGo.GetInstanceID(),
                TargetName = targetGo.name,
                BoundComponentType = boundComponentType,
            });
        }

        private static ToolResult<TimelineBindResult> SetReference(TimelineBindParams p, PlayableDirector director, TrackAsset track)
        {
            var clips = track.GetClips().ToList();
            if (p.ClipIndex < 0 || p.ClipIndex >= clips.Count)
                return ToolResult<TimelineBindResult>.Fail(
                    $"ClipIndex {p.ClipIndex} is out of range (0..{clips.Count - 1})", ErrorCodes.OUT_OF_RANGE);

            var clip = clips[p.ClipIndex];
            if (!(clip.asset is ControlPlayableAsset controlAsset))
                return ToolResult<TimelineBindResult>.Fail(
                    $"set-reference only supports ControlPlayableAsset.sourceGameObject currently " +
                    $"— clip {p.ClipIndex} on track {p.TrackIndex} is a {clip.asset?.GetType().Name ?? "null"}.",
                    ErrorCodes.INVALID_PARAM);

            if (!GameObjectResolver.TryResolve(p.ReferenceInstanceId, p.ReferencePath, out var referenceGo, out var referenceError))
                return ToolResult<TimelineBindResult>.Fail(referenceError, ErrorCodes.NOT_FOUND);

            Undo.RecordObject(director, "Mosaic: Set Timeline ExposedReference");
            director.SetReferenceValue(controlAsset.sourceGameObject.exposedName, referenceGo);
            EditorUtility.SetDirty(director);

            return ToolResult<TimelineBindResult>.Ok(new TimelineBindResult
            {
                Action = "set-reference",
                DirectorInstanceId = director.gameObject.GetInstanceID(),
                TrackIndex = p.TrackIndex,
                TrackName = track.name,
                ClipIndex = p.ClipIndex,
                ReferenceField = "sourceGameObject",
                ReferenceTargetName = referenceGo.name,
            });
        }
    }
}
#endif

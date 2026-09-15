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
        // Cinemachine 2's Timeline integration and Cinemachine 3's both ship this type under a
        // different assembly; tried in order, first match wins. No compile-time reference either
        // way — resolved at runtime so this bridge carries no Cinemachine dependency.
        private static readonly string[] CinemachineTrackCandidates =
        {
            "Unity.Cinemachine.CinemachineTrack, Unity.Cinemachine",
            "Cinemachine.CinemachineTrack, Cinemachine",
        };

        [MosaicTool("timeline/add-track",
                    "Adds a track to a TimelineAsset. TrackType: Animation, Audio, Activation, " +
                    "Signal, Control, Group, Marker (the timeline's own global marker track — " +
                    "idempotent), Playable, Cinemachine (needs the Cinemachine package), or Custom " +
                    "(with CustomTypeName — any TrackAsset-derived type, by assembly-qualified or " +
                    "bare name). ParentGroupName nests the new track under an existing GroupTrack.",
                    isReadOnly: false)]
        public static ToolResult<TimelineAddTrackResult> AddTrack(TimelineAddTrackParams p)
        {
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(p.AssetPath);
            if (timeline == null)
                return ToolResult<TimelineAddTrackResult>.Fail(
                    $"TimelineAsset not found at '{p.AssetPath}'", ErrorCodes.NOT_FOUND);

            if (string.Equals(p.TrackType, "marker", StringComparison.OrdinalIgnoreCase))
            {
                if (timeline.markerTrack == null)
                    timeline.CreateMarkerTrack();
                EditorUtility.SetDirty(timeline);
                AssetDatabase.SaveAssets();
                return ToolResult<TimelineAddTrackResult>.Ok(new TimelineAddTrackResult
                {
                    TrackIndex = -1, TrackType = "Marker", Name = timeline.markerTrack.name,
                });
            }

            GroupTrack parentGroup = null;
            if (!string.IsNullOrEmpty(p.ParentGroupName))
            {
                parentGroup = FindGroupTrackByName(timeline, p.ParentGroupName);
                if (parentGroup == null)
                    return ToolResult<TimelineAddTrackResult>.Fail(
                        $"No GroupTrack named '{p.ParentGroupName}' found in '{p.AssetPath}'", ErrorCodes.NOT_FOUND);
            }

            TrackAsset track;
            switch (p.TrackType?.ToLowerInvariant())
            {
                case "animation":
                    track = timeline.CreateTrack<AnimationTrack>(parentGroup, p.Name);
                    break;
                case "audio":
                    track = timeline.CreateTrack<AudioTrack>(parentGroup, p.Name);
                    break;
                case "activation":
                    track = timeline.CreateTrack<ActivationTrack>(parentGroup, p.Name);
                    break;
                case "signal":
                    track = timeline.CreateTrack<SignalTrack>(parentGroup, p.Name);
                    break;
                case "control":
                    track = timeline.CreateTrack<ControlTrack>(parentGroup, p.Name);
                    break;
                case "group":
                    track = timeline.CreateTrack<GroupTrack>(parentGroup, p.Name);
                    break;
                case "playable":
                    track = timeline.CreateTrack<PlayableTrack>(parentGroup, p.Name);
                    break;
                case "cinemachine":
                    if (!TryResolveTrackType(CinemachineTrackCandidates, out var cmType))
                        return ToolResult<TimelineAddTrackResult>.Fail(
                            "Cinemachine's CinemachineTrack type could not be found — the Cinemachine " +
                            "package is not installed.", ErrorCodes.NOT_FOUND);
                    track = timeline.CreateTrack(cmType, parentGroup, p.Name);
                    break;
                case "custom":
                    if (string.IsNullOrEmpty(p.CustomTypeName))
                        return ToolResult<TimelineAddTrackResult>.Fail(
                            "CustomTypeName is required when TrackType is Custom", ErrorCodes.INVALID_PARAM);
                    if (!TryResolveTrackType(new[] { p.CustomTypeName }, out var customType))
                        return ToolResult<TimelineAddTrackResult>.Fail(
                            $"TrackAsset-derived type '{p.CustomTypeName}' not found", ErrorCodes.NOT_FOUND);
                    track = timeline.CreateTrack(customType, parentGroup, p.Name);
                    break;
                default:
                    return ToolResult<TimelineAddTrackResult>.Fail(
                        $"Unknown track type '{p.TrackType}'. Valid types: Animation, Audio, Activation, " +
                        "Signal, Control, Group, Marker, Playable, Cinemachine, Custom",
                        ErrorCodes.INVALID_PARAM);
            }

            if (p.Mute.HasValue)
                track.muted = p.Mute.Value;

            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();

            int trackIndex = timeline.GetOutputTracks().ToList().IndexOf(track);

            return ToolResult<TimelineAddTrackResult>.Ok(new TimelineAddTrackResult
            {
                TrackIndex = trackIndex,
                TrackType = p.TrackType,
                Name = track.name,
                Muted = track.muted,
                ParentGroupName = parentGroup?.name,
            });
        }

        private static GroupTrack FindGroupTrackByName(TimelineAsset timeline, string name) =>
            timeline.GetRootTracks().Select(root => FindGroupRecursive(root, name)).FirstOrDefault(found => found != null);

        private static GroupTrack FindGroupRecursive(TrackAsset track, string name)
        {
            if (track is GroupTrack g && track.name == name) return g;
            return track.GetChildTracks().Select(child => FindGroupRecursive(child, name)).FirstOrDefault(found => found != null);
        }

        private static bool TryResolveTrackType(string[] candidateNames, out Type result)
        {
            foreach (var name in candidateNames)
            {
                var direct = Type.GetType(name);
                if (direct != null && typeof(TrackAsset).IsAssignableFrom(direct))
                {
                    result = direct;
                    return true;
                }
            }

            // Bare name fallback: search every loaded assembly, matching Name or FullName —
            // same pattern as ResolveComponentType/ResolveStateMachineBehaviourType.
            foreach (var name in candidateNames)
            {
                var match = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                    .Where(t => typeof(TrackAsset).IsAssignableFrom(t) && (t.Name == name || t.FullName == name))
                    .ToList();
                if (match.Count > 0)
                {
                    result = match.FirstOrDefault(t => t.FullName == name) ?? match[0];
                    return true;
                }
            }

            result = null;
            return false;
        }
    }
}
#endif

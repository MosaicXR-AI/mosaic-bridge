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

            // L3: ClipAssetPath used to be silently ignored for every track type except
            // AnimationTrack — an Audio clip was created empty (plays nothing) and a Control
            // clip got no prefab, with no error telling the caller why. Wire the loaded asset
            // onto the clip's PlayableAsset per track type, and fail loudly (not silently) when
            // the asset's type doesn't match what the track type actually needs.
            if (!string.IsNullOrEmpty(p.ClipAssetPath))
            {
                var clipAsset = AssetDatabase.LoadAssetAtPath<Object>(p.ClipAssetPath);
                if (clipAsset == null)
                    return ToolResult<TimelineAddClipResult>.Fail(
                        $"Clip asset not found at '{p.ClipAssetPath}'", ErrorCodes.NOT_FOUND);

                if (track is AnimationTrack animTrack)
                {
                    if (!(clipAsset is AnimationClip animClip))
                        return ToolResult<TimelineAddClipResult>.Fail(
                            $"ClipAssetPath '{p.ClipAssetPath}' is a {clipAsset.GetType().Name}, not an " +
                            "AnimationClip — required for a clip on an AnimationTrack.", ErrorCodes.INVALID_PARAM);
                    clip = animTrack.CreateClip(animClip);
                }
                else if (track is AudioTrack)
                {
                    if (!(clipAsset is AudioClip audioClip))
                        return ToolResult<TimelineAddClipResult>.Fail(
                            $"ClipAssetPath '{p.ClipAssetPath}' is a {clipAsset.GetType().Name}, not an " +
                            "AudioClip — required for a clip on an AudioTrack.", ErrorCodes.INVALID_PARAM);
                    clip = track.CreateDefaultClip();
                    ((AudioPlayableAsset)clip.asset).clip = audioClip;
                }
                else if (track is ControlTrack)
                {
                    if (!(clipAsset is GameObject prefabGo))
                        return ToolResult<TimelineAddClipResult>.Fail(
                            $"ClipAssetPath '{p.ClipAssetPath}' is a {clipAsset.GetType().Name}, not a " +
                            "prefab GameObject — required for a clip on a ControlTrack.", ErrorCodes.INVALID_PARAM);
                    clip = track.CreateDefaultClip();
                    // prefabGameObject (not sourceGameObject, which is an ExposedReference to a
                    // SCENE object and cannot be resolved from a prefab asset) is the field
                    // ControlPlayableAsset exposes precisely for "control a prefab, instantiated
                    // at runtime" — https://docs.unity3d.com/ScriptReference/Timeline.ControlPlayableAsset-prefabGameObject.html
                    ((ControlPlayableAsset)clip.asset).prefabGameObject = prefabGo;
                }
                else
                {
                    return ToolResult<TimelineAddClipResult>.Fail(
                        $"ClipAssetPath is not supported on track type {track.GetType().Name} — only " +
                        "AnimationTrack, AudioTrack, and ControlTrack clips can be created from an existing " +
                        "asset via this tool. Omit ClipAssetPath for a default (empty) clip.",
                        ErrorCodes.INVALID_PARAM);
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

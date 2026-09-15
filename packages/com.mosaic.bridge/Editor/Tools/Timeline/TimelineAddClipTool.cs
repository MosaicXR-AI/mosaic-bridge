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
                    "Adds a clip to a track in a TimelineAsset. TimelineClip fields (DisplayName, " +
                    "ClipIn, TimeScale, Ease/BlendIn/OutDuration, Blend*CurveMode='Auto'|'Manual') " +
                    "apply to any track. Per-track typed fields require a matching ClipAssetPath: " +
                    "AnimationTrack — AnimPosition/AnimEulerAngles/AnimLoop='Off'|'On'|'UseSourceAsset'" +
                    "/AnimRemoveStartOffset/AnimApplyFootIK; AudioTrack — AudioLoop; ControlTrack — " +
                    "ControlUpdateParticle/ControlPostPlayback='Active'|'Inactive'|'Revert'.",
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

            if (!string.IsNullOrEmpty(p.DisplayName))
                clip.displayName = p.DisplayName;
            if (p.ClipIn.HasValue)
                clip.clipIn = p.ClipIn.Value;
            if (p.TimeScale.HasValue)
                clip.timeScale = p.TimeScale.Value;
            if (p.EaseInDuration.HasValue)
                clip.easeInDuration = p.EaseInDuration.Value;
            if (p.EaseOutDuration.HasValue)
                clip.easeOutDuration = p.EaseOutDuration.Value;
            if (p.BlendInDuration.HasValue)
                clip.blendInDuration = p.BlendInDuration.Value;
            if (p.BlendOutDuration.HasValue)
                clip.blendOutDuration = p.BlendOutDuration.Value;

            if (!string.IsNullOrEmpty(p.BlendInCurveMode))
            {
                if (!TryParseBlendCurveMode(p.BlendInCurveMode, out var mode))
                    return ToolResult<TimelineAddClipResult>.Fail(
                        $"Unknown BlendInCurveMode '{p.BlendInCurveMode}'. Valid: Auto, Manual", ErrorCodes.INVALID_PARAM);
                clip.blendInCurveMode = mode;
            }
            if (!string.IsNullOrEmpty(p.BlendOutCurveMode))
            {
                if (!TryParseBlendCurveMode(p.BlendOutCurveMode, out var mode))
                    return ToolResult<TimelineAddClipResult>.Fail(
                        $"Unknown BlendOutCurveMode '{p.BlendOutCurveMode}'. Valid: Auto, Manual", ErrorCodes.INVALID_PARAM);
                clip.blendOutCurveMode = mode;
            }

            if (clip.asset is AnimationPlayableAsset animAsset)
            {
                if (p.AnimPosition != null)
                {
                    if (p.AnimPosition.Length != 3)
                        return ToolResult<TimelineAddClipResult>.Fail(
                            "AnimPosition requires exactly [x, y, z]", ErrorCodes.INVALID_PARAM);
                    animAsset.position = new Vector3(p.AnimPosition[0], p.AnimPosition[1], p.AnimPosition[2]);
                }
                if (p.AnimEulerAngles != null)
                {
                    if (p.AnimEulerAngles.Length != 3)
                        return ToolResult<TimelineAddClipResult>.Fail(
                            "AnimEulerAngles requires exactly [x, y, z]", ErrorCodes.INVALID_PARAM);
                    animAsset.eulerAngles = new Vector3(p.AnimEulerAngles[0], p.AnimEulerAngles[1], p.AnimEulerAngles[2]);
                }
                if (!string.IsNullOrEmpty(p.AnimLoop))
                {
                    if (!TryParseAnimLoopMode(p.AnimLoop, out var loopMode))
                        return ToolResult<TimelineAddClipResult>.Fail(
                            $"Unknown AnimLoop '{p.AnimLoop}'. Valid: Off, On, UseSourceAsset", ErrorCodes.INVALID_PARAM);
                    animAsset.loop = loopMode;
                }
                if (p.AnimRemoveStartOffset.HasValue)
                    animAsset.removeStartOffset = p.AnimRemoveStartOffset.Value;
                if (p.AnimApplyFootIK.HasValue)
                    animAsset.applyFootIK = p.AnimApplyFootIK.Value;
            }
            else if (clip.asset is AudioPlayableAsset audioAsset)
            {
                if (p.AudioLoop.HasValue)
                    audioAsset.loop = p.AudioLoop.Value;
            }
            else if (clip.asset is ControlPlayableAsset controlAsset)
            {
                if (p.ControlUpdateParticle.HasValue)
                    controlAsset.updateParticle = p.ControlUpdateParticle.Value;
                if (!string.IsNullOrEmpty(p.ControlPostPlayback))
                {
                    if (!TryParsePostPlaybackState(p.ControlPostPlayback, out var state))
                        return ToolResult<TimelineAddClipResult>.Fail(
                            $"Unknown ControlPostPlayback '{p.ControlPostPlayback}'. Valid: Active, Inactive, Revert",
                            ErrorCodes.INVALID_PARAM);
                    controlAsset.postPlayback = state;
                }
            }

            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssets();

            return ToolResult<TimelineAddClipResult>.Ok(new TimelineAddClipResult
            {
                TrackIndex = p.TrackIndex,
                ClipName = clip.displayName,
                Start = clip.start,
                Duration = clip.duration,
                ClipIn = clip.clipIn,
                TimeScale = clip.timeScale,
                EaseInDuration = clip.easeInDuration,
                EaseOutDuration = clip.easeOutDuration,
                BlendInDuration = clip.blendInDuration,
                BlendOutDuration = clip.blendOutDuration,
                BlendInCurveMode = clip.blendInCurveMode.ToString(),
                BlendOutCurveMode = clip.blendOutCurveMode.ToString(),
            });
        }

        private static bool TryParseBlendCurveMode(string value, out TimelineClip.BlendCurveMode result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "auto":   result = TimelineClip.BlendCurveMode.Auto;   return true;
                case "manual": result = TimelineClip.BlendCurveMode.Manual; return true;
                default:       result = TimelineClip.BlendCurveMode.Auto;   return false;
            }
        }

        private static bool TryParseAnimLoopMode(string value, out AnimationPlayableAsset.LoopMode result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "off":            result = AnimationPlayableAsset.LoopMode.Off;            return true;
                case "on":             result = AnimationPlayableAsset.LoopMode.On;              return true;
                case "usesourceasset": result = AnimationPlayableAsset.LoopMode.UseSourceAsset;  return true;
                default:               result = AnimationPlayableAsset.LoopMode.Off;             return false;
            }
        }

        private static bool TryParsePostPlaybackState(string value, out ActivationControlPlayable.PostPlaybackState result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "active":   result = ActivationControlPlayable.PostPlaybackState.Active;   return true;
                case "inactive": result = ActivationControlPlayable.PostPlaybackState.Inactive; return true;
                case "revert":   result = ActivationControlPlayable.PostPlaybackState.Revert;   return true;
                default:         result = ActivationControlPlayable.PostPlaybackState.Active;   return false;
            }
        }
    }
}
#endif

#if MOSAIC_HAS_TIMELINE
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;
using Mosaic.Bridge.Contracts.Compat;

namespace Mosaic.Bridge.Tools.Timeline
{
    public static class TimelineSetDirectorTool
    {
        [MosaicTool("timeline/set-director",
                    "Configures a PlayableDirector on a GameObject with the specified TimelineAsset",
                    isReadOnly: false)]
        public static ToolResult<TimelineSetDirectorResult> SetDirector(TimelineSetDirectorParams p)
        {
            // Resolve the target GameObject
            GameObject go = null;
            if (p.InstanceId != 0)
                go = UnityIds.Resolve(p.InstanceId) as GameObject;
            if (go == null && !string.IsNullOrEmpty(p.Name))
                go = GameObject.Find(p.Name);
            if (go == null)
                return ToolResult<TimelineSetDirectorResult>.Fail(
                    "GameObject not found. Provide a valid InstanceId or Name.",
                    ErrorCodes.NOT_FOUND);

            // Load the timeline asset
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(p.TimelineAssetPath);
            if (timeline == null)
                return ToolResult<TimelineSetDirectorResult>.Fail(
                    $"TimelineAsset not found at '{p.TimelineAssetPath}'", ErrorCodes.NOT_FOUND);

            // Add or get PlayableDirector component
            var director = go.GetComponent<PlayableDirector>();
            if (director == null)
            {
                director = Undo.AddComponent<PlayableDirector>(go);
            }
            else
            {
                Undo.RecordObject(director, "Mosaic: Set Director Timeline");
            }

            director.playableAsset = timeline;

            if (p.PlayOnAwake.HasValue)
                director.playOnAwake = p.PlayOnAwake.Value;

            if (!string.IsNullOrEmpty(p.WrapMode))
            {
                if (!TryParseWrapMode(p.WrapMode, out var wrapMode))
                    return ToolResult<TimelineSetDirectorResult>.Fail(
                        $"Unknown WrapMode '{p.WrapMode}'. Valid: Hold, Loop, None", ErrorCodes.INVALID_PARAM);
                director.extrapolationMode = wrapMode;
            }

            if (!string.IsNullOrEmpty(p.UpdateMode))
            {
                if (!TryParseUpdateMode(p.UpdateMode, out var updateMode))
                    return ToolResult<TimelineSetDirectorResult>.Fail(
                        $"Unknown UpdateMode '{p.UpdateMode}'. Valid: GameTime, DSPClock, UnscaledGameTime, Manual",
                        ErrorCodes.INVALID_PARAM);
                director.timeUpdateMode = updateMode;
            }

            if (p.InitialTime.HasValue)
                director.initialTime = p.InitialTime.Value;

            EditorUtility.SetDirty(director);

            return ToolResult<TimelineSetDirectorResult>.Ok(new TimelineSetDirectorResult
            {
                InstanceId = UnityIds.Of(go),
                GameObjectName = go.name,
                TimelineAssetPath = p.TimelineAssetPath,
                PlayOnAwake = director.playOnAwake,
                WrapMode = director.extrapolationMode.ToString(),
                UpdateMode = director.timeUpdateMode.ToString(),
                InitialTime = director.initialTime,
            });
        }

        private static bool TryParseWrapMode(string value, out DirectorWrapMode result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "hold": result = DirectorWrapMode.Hold; return true;
                case "loop": result = DirectorWrapMode.Loop; return true;
                case "none": result = DirectorWrapMode.None; return true;
                default:     result = DirectorWrapMode.Hold; return false;
            }
        }

        private static bool TryParseUpdateMode(string value, out DirectorUpdateMode result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "gametime":         result = DirectorUpdateMode.GameTime;         return true;
                case "dspclock":         result = DirectorUpdateMode.DSPClock;         return true;
                case "unscaledgametime": result = DirectorUpdateMode.UnscaledGameTime; return true;
                case "manual":           result = DirectorUpdateMode.Manual;           return true;
                default:                 result = DirectorUpdateMode.GameTime;         return false;
            }
        }
    }
}
#endif

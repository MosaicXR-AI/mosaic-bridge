#if MOSAIC_HAS_TIMELINE
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Timeline
{
    public static class TimelineCreateTool
    {
        [MosaicTool("timeline/create",
                    "Creates a new TimelineAsset at the specified path",
                    isReadOnly: false)]
        public static ToolResult<TimelineCreateResult> Create(TimelineCreateParams p)
        {
            if (!p.Path.StartsWith("Assets/"))
                return ToolResult<TimelineCreateResult>.Fail(
                    "Path must start with 'Assets/'", ErrorCodes.INVALID_PARAM);

            if (!p.Path.EndsWith(".playable"))
                p.Path = p.Path + ".playable";

            // Ensure parent directory exists
            var dir = System.IO.Path.GetDirectoryName(p.Path);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                // Create folders recursively
                var parts = dir.Replace("\\", "/").Split('/');
                var current = parts[0]; // "Assets"
                for (int i = 1; i < parts.Length; i++)
                {
                    var next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }

            var timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            timeline.name = p.Name;

            if (p.FrameRate.HasValue)
                timeline.editorSettings.frameRate = p.FrameRate.Value;

            if (!string.IsNullOrEmpty(p.DurationMode))
            {
                if (!TryParseDurationMode(p.DurationMode, out var durationMode))
                    return ToolResult<TimelineCreateResult>.Fail(
                        $"Unknown DurationMode '{p.DurationMode}'. Valid: BasedOnClips, FixedLength",
                        ErrorCodes.INVALID_PARAM);
                timeline.durationMode = durationMode;
            }

            if (p.FixedDuration.HasValue)
                timeline.fixedDuration = p.FixedDuration.Value;

            AssetDatabase.CreateAsset(timeline, p.Path);
            AssetDatabase.SaveAssets();

            Undo.RegisterCreatedObjectUndo(timeline, "Mosaic: Create Timeline");

            return ToolResult<TimelineCreateResult>.Ok(new TimelineCreateResult
            {
                AssetPath = p.Path,
                Name = p.Name,
                FrameRate = timeline.editorSettings.frameRate,
                DurationMode = timeline.durationMode.ToString(),
                FixedDuration = timeline.fixedDuration,
            });
        }

        private static bool TryParseDurationMode(string value, out TimelineAsset.DurationMode result)
        {
            switch (value?.Trim().ToLowerInvariant())
            {
                case "basedonclips": result = TimelineAsset.DurationMode.BasedOnClips; return true;
                case "fixedlength":  result = TimelineAsset.DurationMode.FixedLength;  return true;
                default:             result = TimelineAsset.DurationMode.BasedOnClips; return false;
            }
        }
    }
}
#endif

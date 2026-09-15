using UnityEngine;
using UnityEditor;
using Mosaic.Bridge.Contracts.Attributes;
using Mosaic.Bridge.Contracts.Envelopes;
using Mosaic.Bridge.Contracts.Errors;

namespace Mosaic.Bridge.Tools.Audio
{
    public static class AudioPreviewTool
    {
        [MosaicTool("audio/preview",
                    "Plays/stops/queries edit-mode AudioClip preview (UnityEditor.AudioUtil), to verify " +
                    "a generated clip is audible and the right length before wiring it up. Real audio " +
                    "output — skip in CI.",
                    isReadOnly: false)]
        public static ToolResult<AudioPreviewResult> Execute(AudioPreviewParams p)
        {
            switch (p.Action?.ToLowerInvariant())
            {
                case "play":
                {
                    if (string.IsNullOrEmpty(p.ClipPath))
                        return ToolResult<AudioPreviewResult>.Fail("ClipPath is required for 'play'", ErrorCodes.INVALID_PARAM);

                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(p.ClipPath);
                    if (clip == null)
                        return ToolResult<AudioPreviewResult>.Fail(
                            $"AudioClip not found at '{p.ClipPath}'", ErrorCodes.NOT_FOUND);

                    if (!AudioPreviewReflection.TryPlay(clip, p.StartSample, p.Loop, out string playError))
                        return ToolResult<AudioPreviewResult>.Fail(playError, ErrorCodes.NOT_PERMITTED);

                    return ToolResult<AudioPreviewResult>.Ok(new AudioPreviewResult
                    {
                        Action = "play", IsPlaying = true,
                        Message = $"Playing '{p.ClipPath}' (length {clip.length:F2}s)"
                    });
                }

                case "stop":
                {
                    if (!AudioPreviewReflection.TryStop(out string stopError))
                        return ToolResult<AudioPreviewResult>.Fail(stopError, ErrorCodes.NOT_PERMITTED);

                    return ToolResult<AudioPreviewResult>.Ok(new AudioPreviewResult
                    {
                        Action = "stop", IsPlaying = false, Message = "Stopped all preview clips"
                    });
                }

                case "status":
                {
                    if (!AudioPreviewReflection.TryGetStatus(out bool isPlaying, out float position, out string statusError))
                        return ToolResult<AudioPreviewResult>.Fail(statusError, ErrorCodes.NOT_PERMITTED);

                    return ToolResult<AudioPreviewResult>.Ok(new AudioPreviewResult
                    {
                        Action = "status", IsPlaying = isPlaying, PositionSeconds = position,
                        Message = isPlaying ? $"Playing at {position:F2}s" : "Nothing playing"
                    });
                }

                default:
                    return ToolResult<AudioPreviewResult>.Fail(
                        $"Unknown action '{p.Action}'. Valid actions: play, stop, status", ErrorCodes.INVALID_PARAM);
            }
        }
    }
}
